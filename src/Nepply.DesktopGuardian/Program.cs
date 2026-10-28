using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Nepply.Helpers;
using Nepply.Services;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

internal static class Program
{
    // Strict fixed-size pipe protocol. The optional journal launch argument is
    // validated only after authenticating the owner; no arbitrary commands accepted.
    private const byte Arm = 1, Disarm = 2, Restore = 3, Ready = 128, Success = 129, Failure = 130;
    private static int Main(string[] args)
    {
        if (args.Length is not (3 or 4) || !Guid.TryParseExact(args[0], "N", out var generation) || generation == Guid.Empty
            || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid <= 0
            || !long.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) || ticks <= 0) return 2;
        try
        {
            // A guardian inside the owner's package lifetime cannot protect removal.
            // Refuse to arm rather than advertise recovery we cannot provide.
            if (ProcessPackageIdentity.IsPackaged) return 2;
            using var owner = Process.GetProcessById(pid);
            _ = owner.SafeHandle;
            using var self = Process.GetCurrentProcess();
            if (owner.StartTime.ToUniversalTime().Ticks != ticks || owner.SessionId != self.SessionId || owner.HasExited) return 2;
            using var pipe = new NamedPipeClientStream(".", "Nepply-guardian-" + generation.ToString("N"), PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using (var connect = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                pipe.ConnectAsync(connect.Token).GetAwaiter().GetResult();
            HelperPipeIdentity.RequireServer(pipe, owner);
            string? journal = args.Length == 4 ? args[3] : null;
            if (journal is not null && !DesktopRecoveryJournal.IsAllowedPath(journal,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))) return 2;
            // Hold on this synchronous thread until async recovery is fully complete.
            using var fence = DesktopGuardianFence.Acquire(TimeSpan.FromSeconds(15));
            if (owner.HasExited) return 2;
            return Serve(pipe, owner, generation, journal).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
            or ArgumentException or InvalidOperationException or OperationCanceledException or TimeoutException)
        { return 3; }
    }

    private static async Task<int> Serve(NamedPipeClientStream pipe, Process owner, Guid generation, string? journal)
    {
        var desktop = new ExplorerDesktopVisibility();
        var session = new DesktopRecoverySession(generation, desktop.IsVisible, () => desktop.SetVisible(true));
        using var lifetime = new CancellationTokenSource();
        var ownerExit = owner.WaitForExitAsync(lifetime.Token);
        byte[] request = new byte[1];
        byte[] response = new byte[1];
        try
        {
            await Send(Ready);
            while (true)
            {
                // Before arm, an idle/malformed peer cannot keep an unused helper alive.
                using var readLimit = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                if (!session.IsArmed) readLimit.CancelAfter(TimeSpan.FromSeconds(10));
                Task<int> read = pipe.ReadAsync(request, readLimit.Token).AsTask();
                if (await Task.WhenAny(read, ownerExit) == ownerExit)
                {
                    readLimit.Cancel();
                    try { await read; } catch (OperationCanceledException) { } catch (IOException) { }
                    break;
                }
                if (await read != 1) break;
                bool ok;
                switch (request[0])
                {
                    case Arm: ok = journal is not null && session.Arm(generation); break;
                    case Disarm:
                        ok = session.Disarm(generation);
                        await Send(ok ? Success : Failure);
                        if (ok) return 0;
                        continue;
                    case Restore:
                        ok = session.Recover(generation);
                        await Send(ok ? Success : Failure);
                        if (ok) return 0;
                        continue;
                    default: return 4;
                }
                await Send(ok ? Success : Failure);
            }
            return 0;
        }
        finally
        {
            // Pipe loss alone doesn't authorize racing an owner that can still hide.
            // Retain the guard until that owner exits; production caller must fence next owner.
            if (session.IsArmed || session.IsCompleted)
            {
                if (session.IsArmed) await ownerExit;
                var deadline = Stopwatch.StartNew();
                while (true)
                {
                    if (session.Recover(generation) && ClearJournal()) break;
                    if (deadline.Elapsed >= TimeSpan.FromSeconds(10)) throw new IOException("Desktop recovery deadline exceeded.");
                    await Task.Delay(100);
                }
            }
            lifetime.Cancel();
            try { await ownerExit; } catch (OperationCanceledException) { }
        }

        async Task Send(byte value)
        {
            response[0] = value;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await pipe.WriteAsync(response, timeout.Token);
        }

        bool ClearJournal()
        {
            try { return journal is not null && DesktopRecoveryJournal.ClearCompleted(journal); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
