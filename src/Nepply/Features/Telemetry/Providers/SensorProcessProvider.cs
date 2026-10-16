using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Nepply.Models;

namespace Nepply.Services.Telemetry;

/// <summary>Owns exactly one child. Native driver calls never run in the panel process.</summary>
public sealed class SensorProcessProvider : IHardwareProvider
{
    private readonly string _executable;
    private Process? _child;
    internal Process? DiagnosticProcess => _child;
    private NamedPipeServerStream? _pipe;
    internal sealed record Request(int Version,string GpuIdentifier);
    internal sealed record Response(int Version,HardwareReading Reading);
    public string Name => "Isolated native sensors";
    public SensorProcessProvider(string? executable = null) => _executable = executable ?? Environment.ProcessPath!;

    public HardwareReading Read(AppSettings settings) => Read(settings,CancellationToken.None);
    public HardwareReading Read(AppSettings settings,CancellationToken cancellation)
    {
        // Includes process startup and first driver enumeration; a stuck driver cannot block indefinitely.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try { return ReadAsync(settings, deadline.Token).GetAwaiter().GetResult(); }
        catch { Dispose(); throw; }
    }

    private async Task<HardwareReading> ReadAsync(AppSettings settings, CancellationToken token)
    {
        if (_pipe == null)
        {
            var name = "Nepply-sensors-" + Guid.NewGuid().ToString("N");
            _pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var start = new ProcessStartInfo(_executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add("--sensor-host"); start.ArgumentList.Add(name);
            start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _child = Process.Start(start) ?? throw new IOException("Sensor process did not start.");
            await _pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
            Helpers.HelperPipeIdentity.RequireClient(_pipe,_child.Id);
        }
        await SensorWireProtocol.WriteAsync(_pipe,new Request(1,settings.GpuIdentifier),SensorWireProtocol.RequestLimit,token).ConfigureAwait(false);
        var response=await SensorWireProtocol.ReadAsync<Response>(_pipe,SensorWireProtocol.ResponseLimit,token).ConfigureAwait(false)
            ?? throw new IOException("Sensor process disconnected.");
        if(response.Version!=1 || response.Reading==null) throw new InvalidDataException("Unsupported sensor response.");
        var reading=response.Reading;
        // Freshness is measured by the receiving process, not by a remote timestamp.
        return reading with { MonotonicTimestamp = Stopwatch.GetTimestamp() };
    }

    public void Dispose()
    {
        // Terminate only the process this instance started, never enumerate/kill by process name.
        try
        {
            // EOF lets the normal path close driver handles cleanly. Only a stuck child is killed.
            _pipe?.Dispose();
            if (_child is { HasExited: false } && !_child.WaitForExit(300)) _child.Kill();
        } catch (InvalidOperationException) { }
        finally
        {
            _pipe?.Dispose(); _pipe = null;
            _child?.Dispose(); _child = null;
        }
    }

    public static void RunHost(string pipeName, int parentId)
    {
        using var parent = Process.GetProcessById(parentId);
        // If the UI crashes during a blocked native call, do not leave a sensor process behind.
        using var watchdog = new ParentExitSubscription(parent, () => Environment.Exit(0));
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        pipe.Connect(10000);
        Helpers.HelperPipeIdentity.RequireServer(pipe,parent);
        using IHardwareProvider provider = new LibreHardwareProvider();
        while (true)
        {
            using var idle = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var request=SensorWireProtocol.ReadAsync<Request>(pipe,SensorWireProtocol.RequestLimit,idle.Token).GetAwaiter().GetResult();
            if(request==null) return;
            if(request.Version!=1 || request.GpuIdentifier==null || request.GpuIdentifier.Length>2048) throw new InvalidDataException("Unsupported sensor request.");
            var settings = new AppSettings { GpuIdentifier=request.GpuIdentifier };
            settings.Validate();
            SensorWireProtocol.WriteAsync(pipe,new Response(1,provider.Read(settings)),SensorWireProtocol.ResponseLimit,idle.Token).GetAwaiter().GetResult();
        }
    }
}
