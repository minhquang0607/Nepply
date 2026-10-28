using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using Nepply.Helpers;

namespace Nepply.Services;

/// <summary>Authenticated single-session connection. Caller owns deployment and Desktop lifecycle.</summary>
internal sealed class DesktopGuardianConnection : IDesktopGuardianSession
{
    private readonly NamedPipeServerStream _pipe;
    private readonly Process _process;
    private readonly SemaphoreSlim _commands = new(1, 1);
    private bool _mayBeArmed, _broken, _disposed;
    private readonly Task _exit;
    public Task Exited => _exit;
    internal int ProcessId => _process.Id;

    private DesktopGuardianConnection(NamedPipeServerStream pipe, Process process)
    {
        _pipe = pipe;
        _process = process;
        _exit = process.WaitForExitAsync();
    }

    internal static async Task<DesktopGuardianConnection> StartAsync(string verifiedExecutable, CancellationToken cancellation = default, string? journal = null)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(verifiedExecutable)) throw new ArgumentException("Guardian requires an absolute verified payload path.", nameof(verifiedExecutable));
        Guid generation = Guid.NewGuid();
        var pipe = new NamedPipeServerStream("Nepply-guardian-" + generation.ToString("N"), PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Process? process = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(25)); // Includes old guardian's bounded recovery/fence wait.
        try
        {
            using var self = Process.GetCurrentProcess();
            var start = new ProcessStartInfo(verifiedExecutable) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(generation.ToString("N"));
            start.ArgumentList.Add(self.Id.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add(self.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
            if (journal is not null) start.ArgumentList.Add(journal);
            process = Process.Start(start) ?? throw new IOException("Cannot start Desktop guardian.");
            _ = process.SafeHandle;
            await pipe.WaitForConnectionAsync(deadline.Token).ConfigureAwait(false);
            HelperPipeIdentity.RequireClient(pipe, process.Id);
            if (process.HasExited) throw new IOException("Guardian exited before authentication.");
            byte[] ready = new byte[1];
            if (await pipe.ReadAsync(ready, deadline.Token).ConfigureAwait(false) != 1 || ready[0] != 128)
                throw new IOException("Invalid guardian readiness response.");
            return new DesktopGuardianConnection(pipe, process);
        }
        catch
        {
            pipe.Dispose();
            // No Arm was sent: terminating this exact child cannot lose recovery ownership.
            if (process is not null)
            {
                try { if (!process.HasExited) process.Kill(); await process.WaitForExitAsync().ConfigureAwait(false); }
                finally { process.Dispose(); }
            }
            throw;
        }
    }

    public Task<bool> ArmAsync(CancellationToken cancellation = default) => CommandAsync(1, cancellation);
    internal Task<bool> DisarmAsync(CancellationToken cancellation = default) => CommandAsync(2, cancellation);
    public Task<bool> RestoreAsync(CancellationToken cancellation = default) => CommandAsync(3, cancellation);

    private async Task<bool> CommandAsync(byte command, CancellationToken cancellation)
    {
        await _commands.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_broken) throw new IOException("Guardian protocol failed; caller must restore locally before releasing ownership.");
            bool previouslyArmed = _mayBeArmed;
            // Mark BEFORE write: losing the reply doesn't prove helper failed to arm.
            if (command == 1) _mayBeArmed = true;
            bool ok = await ExchangeAsync(command, cancellation).ConfigureAwait(false);
            if (command == 1 && !ok) _mayBeArmed = previouslyArmed;
            if (command != 1 && ok) _mayBeArmed = false;
            return ok;
        }
        finally { _commands.Release(); }
    }

    private async Task<bool> ExchangeAsync(byte command, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        byte[] frame = [command];
        try
        {
            await _pipe.WriteAsync(frame, deadline.Token).ConfigureAwait(false);
            if (await _pipe.ReadAsync(frame, deadline.Token).ConfigureAwait(false) != 1 || frame[0] is not (129 or 130))
                throw new IOException("Invalid guardian command response.");
            return frame[0] == 129;
        }
        catch { _broken = true; throw; } // Never reuse a stream with an uncertain request/reply boundary.
    }

    public async ValueTask DisposeAsync()
    {
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            // Do not silently kill/abandon an armed guardian. Caller must restore first.
            if (_mayBeArmed) throw new InvalidOperationException("Restore Desktop before disposing its guardian connection.");
            await CloseUnarmedAsync().ConfigureAwait(false);
        }
        finally { _commands.Release(); }
        // No semaphore Dispose: concurrent callers may still be queued; collection releases it.
    }

    // Caller must stop further hide attempts and verify actual Desktop visibility.
    // Journal absence alone is NOT a valid successful restore callback.
    public async Task<bool> CloseAfterLocalRestoreAsync(Func<bool> restoreAndVerify)
    {
        ArgumentNullException.ThrowIfNull(restoreAndVerify);
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return true;
            if (!restoreAndVerify()) return false;
            _mayBeArmed = false;
            await CloseUnarmedAsync().ConfigureAwait(false);
            return true;
        }
        finally { _commands.Release(); }
    }

    // Called only while the command gate is held and recovery authority is cleared.
    private async Task CloseUnarmedAsync()
    {
        _pipe.Dispose();
        try { await _exit.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
        catch (TimeoutException)
        {
            if (!_process.HasExited) _process.Kill();
            await _exit.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        // If termination fails, retain the process handle so caller can retry cleanup.
        _process.Dispose();
        _disposed = true;
    }
}
