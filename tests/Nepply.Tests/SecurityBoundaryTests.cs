using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using Nepply;
using Nepply.Helpers;
using Nepply.Models;
using Nepply.Services;

internal static class SecurityBoundaryTests
{
    internal static void Run(Action<string, bool> check)
    {
        var assembly = typeof(DesktopIconService).Assembly;
        var policy = assembly.GetCustomAttribute<DefaultDllImportSearchPathsAttribute>();
        check("First-party native imports use System32 by default", policy?.Paths == DllImportSearchPath.System32);
        var imports = assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<DllImportAttribute>() != null).ToArray();
        check("Native import policy audit finds declarations", imports.Length > 0);
        check("No native import overrides System32 with unsafe search paths", imports.All(m =>
            (m.GetCustomAttribute<DefaultDllImportSearchPathsAttribute>() ?? policy)?.Paths == DllImportSearchPath.System32));
        var start = DesktopIconService.CreateLaunchInfo(new DesktopIcon { IsSystemIcon = true, Path = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" });
        check("System icon pins Explorer to Windows directory", Path.IsPathFullyQualified(start.FileName)
            && start.FileName == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        check("System icon target is a single argument", start.ArgumentList.Count == 1 && start.Arguments.Length == 0);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        check("User profile system icon remains one argument", DesktopIconService.CreateLaunchInfo(new DesktopIcon { IsSystemIcon = true, Path = profile }).ArgumentList.Single() == profile);
        var path = @"C:\Desktop\日本語 & test.lnk";
        var file = DesktopIconService.CreateLaunchInfo(new DesktopIcon { Path = path });
        check("Physical item remains Shell association without command concatenation", file.FileName == path && file.UseShellExecute && file.ArgumentList.Count == 0 && file.Arguments.Length == 0);
        VerifyLaunchQueue(check).GetAwaiter().GetResult();
        VerifyPipe(check).GetAwaiter().GetResult();
    }

    private static async Task VerifyLaunchQueue(Action<string, bool> check)
    {
        var queue = new ShellLaunchQueue();
        using var release = new ManualResetEventSlim();
        var caller = Environment.CurrentManagedThreadId;
        var launches = 0;
        var apartment = ApartmentState.Unknown;
        var background = false;
        var launchThread = caller;
        var failure = UserMessage.FromKey("DesktopLaunchError");
        var stuck = queue.Run(@"C:\Desktop\offline.lnk", () =>
        {
            Interlocked.Increment(ref launches);
            launchThread = Environment.CurrentManagedThreadId;
            apartment = Thread.CurrentThread.GetApartmentState();
            background = Thread.CurrentThread.IsBackground;
            release.Wait(TimeSpan.FromSeconds(10));
            return failure;
        });
        check("A launch stuck in Windows does not hold the caller", !stuck.IsCompleted);
        var repeated = queue.Run(@"c:\desktop\OFFLINE.lnk", () => { Interlocked.Increment(ref launches); return failure; });
        check("Repeated clicks during a stuck launch start nothing", repeated.IsCompleted && await repeated == null);
        var other = await queue.Run(@"C:\Desktop\other.lnk", () => null).WaitAsync(TimeSpan.FromSeconds(5));
        check("A stuck launch does not delay other items", other == null && !stuck.IsCompleted);
        release.Set();
        var result = await stuck.WaitAsync(TimeSpan.FromSeconds(5));
        check("Launch runs on its own STA background thread", launchThread != caller && apartment == ApartmentState.STA && background);
        check("Launch failure reaches the caller", ReferenceEquals(result, failure) && Volatile.Read(ref launches) == 1);
        var again = await queue.Run(@"C:\Desktop\offline.lnk", () => { Interlocked.Increment(ref launches); return null; }).WaitAsync(TimeSpan.FromSeconds(5));
        check("An item opens again after its launch ended", again == null && Volatile.Read(ref launches) == 2);
        var thrown = queue.Run(@"C:\Desktop\broken.lnk", () => throw new InvalidOperationException("launch"));
        await Task.WhenAny(thrown, Task.Delay(TimeSpan.FromSeconds(5)));
        check("Unexpected launch exception faults the task", thrown.IsFaulted);
        var retried = await queue.Run(@"C:\Desktop\broken.lnk", () => failure).WaitAsync(TimeSpan.FromSeconds(5));
        check("An item that threw can be opened again", ReferenceEquals(retried, failure));
    }

    private static async Task VerifyPipe(Action<string, bool> check)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        string name = "Nepply-security-test-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await Task.WhenAll(server.WaitForConnectionAsync(timeout.Token), client.ConnectAsync(timeout.Token));
        HelperPipeIdentity.RequireClient(server, Environment.ProcessId);
        check("Kernel pipe identity accepts actual client", true);
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        HelperPipeIdentity.RequireServer(client, current);
        check("Kernel pipe identity accepts actual server", true);
        using var unrelated = System.Diagnostics.Process.GetProcessById(4); // Windows System; never opened for mutation.
        bool wrongServer = false;
        try { HelperPipeIdentity.RequireServer(client, unrelated); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException) { wrongServer = true; }
        check("Kernel pipe identity fails closed for unrelated or inaccessible server", wrongServer);
        bool rejected = false;
        try { HelperPipeIdentity.RequireClient(server, Environment.ProcessId == 1 ? 2 : 1); }
        catch (UnauthorizedAccessException) { rejected = true; }
        check("Kernel pipe identity rejects unrelated process", rejected);
    }
}
