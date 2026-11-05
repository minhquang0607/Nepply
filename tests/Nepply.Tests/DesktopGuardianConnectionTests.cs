using System.IO;
using Nepply.Services;

internal static class DesktopGuardianConnectionTests
{
    internal static async Task Run(string executable, Action<string, bool> check)
    {
        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(executable);
        check("Guardian names its job in Task Manager", info.FileDescription == "Nepply Desktop icon guardian" && info.ProductName == "Nepply");
        var connection = await DesktopGuardianConnection.StartAsync(executable);
        try
        {
            check("Guardian connection authenticated and ready", !connection.Exited.IsCompleted);
            // No Arm: these integration tests must not touch Desktop state.
            check("Guardian connection rejects unarmed restore", !await connection.RestoreAsync());
            check("Guardian connection rejects unarmed disarm", !await connection.DisarmAsync());
            var commands = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => connection.DisarmAsync()));
            check("Guardian connection serializes concurrent commands", commands.All(ok => !ok));
        }
        finally { await connection.DisposeAsync(); }
        check("Guardian connection dispose waits for process exit", connection.Exited.IsCompletedSuccessfully);
        await connection.DisposeAsync();
        bool disposed = false;
        try { await connection.DisarmAsync(); } catch (ObjectDisposedException) { disposed = true; }
        check("Guardian connection rejects use after dispose", disposed);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        bool canceledStart = false;
        try { await using var unused = await DesktopGuardianConnection.StartAsync(executable, canceled.Token); }
        catch (OperationCanceledException) { canceledStart = true; }
        check("Guardian canceled startup cleans its unarmed child", canceledStart);

        // Never armed: only the helper's path check runs, so naming the real journal is safe.
        string journal = DesktopVisibilityLease.JournalPath;
        await using (var accepted = await DesktopGuardianConnection.StartAsync(executable, default, journal))
            check("Guardian accepts the production journal path", !accepted.Exited.IsCompleted);
        string outside = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(journal))!, "Other", "desktop-visibility.lease");
        bool refusedPath = false;
        try { await using var unused = await DesktopGuardianConnection.StartAsync(executable, default, outside); }
        catch (Exception ex) when (ex is System.IO.IOException or System.ComponentModel.Win32Exception) { refusedPath = true; }
        check("Guardian refuses a journal outside the Nepply folder", refusedPath);

        var broken = await DesktopGuardianConnection.StartAsync(executable);
        try
        {
            // Stop only our authenticated, still-unarmed helper. Subsequent Arm cannot reach Desktop.
            using (var child = System.Diagnostics.Process.GetProcessById(broken.ProcessId))
            {
                if (!string.Equals(child.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unexpected QA child executable.");
                child.Kill();
                await child.WaitForExitAsync();
            }
            bool transportFailed = false;
            try { await broken.ArmAsync(); } catch (System.IO.IOException) { transportFailed = true; }
            check("Guardian lost transport leaves arm outcome uncertain", transportFailed);
            bool refused = false;
            try { await broken.DisposeAsync(); } catch (InvalidOperationException) { refused = true; }
            check("Guardian disposal refuses uncertain arm ownership", refused);
            check("Guardian failed local restoration does not close session", !await broken.CloseAfterLocalRestoreAsync(() => false));
            bool threw = false;
            try { await broken.CloseAfterLocalRestoreAsync(() => throw new System.IO.IOException("fixture")); }
            catch (System.IO.IOException) { threw = true; }
            refused = false;
            try { await broken.DisposeAsync(); } catch (InvalidOperationException) { refused = true; }
            check("Guardian throwing local restoration retains ownership", threw && refused);
            check("Guardian verified local restoration allows cleanup", await broken.CloseAfterLocalRestoreAsync(() => true));
            check("Guardian fallback cleanup observes helper exit", broken.Exited.IsCompletedSuccessfully);
        }
        finally { await broken.CloseAfterLocalRestoreAsync(() => true); } // Fixture never armed real Desktop.
        await KilledGuardianIsReported(executable, check);
    }

    // F-06 end to end: a real helper really killed, with the real log. Arm never reaches the
    // helper and Desktop is a fixture, so Explorer is not touched.
    private static async Task KilledGuardianIsReported(string executable, Action<string, bool> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "nepply-guardian-kill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var desktop = new GuardedDesktopVisibilityTests.Desktop();
            var lease = new DesktopVisibilityLease(desktop, Path.Combine(root, "fixture.lease"));
            UnarmedGuardian? guardian = null;
            var flow = new GuardedDesktopVisibility(desktop, lease, async cancellation =>
                guardian = new UnarmedGuardian(await DesktopGuardianConnection.StartAsync(executable, cancellation)));
            using var icons = new DesktopIconService(flow, lease, _ => Task.FromResult(new List<Nepply.Models.DesktopIcon>()));
            var notices = new List<Nepply.UserMessage>();
            icons.DesktopVisibilityFailed += notices.Add;
            long logStart = File.Exists(AppLogger.LogFile) ? new FileInfo(AppLogger.LogFile).Length : 0;

            check("Killed guardian fixture hides with a live helper", await icons.SetDesktopIconsHiddenAsync(true) == null && guardian != null && desktop.Visible == false);
            using (var child = System.Diagnostics.Process.GetProcessById(guardian!.Connection.ProcessId))
            {
                if (!string.Equals(child.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unexpected QA child executable.");
                child.Kill();
            }
            await flow.RecoveryObservation.WaitAsync(TimeSpan.FromSeconds(15));

            check("Killed guardian restores the fixture Desktop", desktop.Visible == true && !lease.OwnsChange && !icons.AreDesktopIconsHidden);
            check("Killed guardian raises one panel notice", notices.Count == 1 && notices[0].Key == "DesktopGuardianExitedNotice");
            string log = await ReadLogUntil(logStart, "Guardian exited; Desktop restored.");
            check("Killed guardian writes its exit code to the real log", log.Contains("ERROR: Desktop guardian exited while Desktop was hidden (exit code 0xFFFFFFFF)."));
            check("Killed guardian writes the outcome to the real log", log.Contains("ERROR: Guardian exited; Desktop restored."));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async Task<string> ReadLogUntil(long start, string text)
    {
        string log = "";
        for (int attempt = 0; attempt < 100 && !log.Contains(text); attempt++)
        {
            await Task.Delay(50);
            if (!File.Exists(AppLogger.LogFile)) continue;
            using var stream = new FileStream(AppLogger.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(Math.Min(start, stream.Length), SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            log = await reader.ReadToEndAsync();
        }
        return log;
    }

    private sealed class UnarmedGuardian(DesktopGuardianConnection connection) : IDesktopGuardianSession
    {
        internal DesktopGuardianConnection Connection => connection;
        public Task Exited => connection.Exited;
        public int? ExitCode => connection.ExitCode;
        public Task<bool> ArmAsync(CancellationToken cancellation = default) => Task.FromResult(true);
        public Task<bool> RestoreAsync(CancellationToken cancellation = default) => connection.RestoreAsync(cancellation);
        public Task<bool> CloseAfterLocalRestoreAsync(Func<bool> restoreAndVerify) => connection.CloseAfterLocalRestoreAsync(restoreAndVerify);
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
