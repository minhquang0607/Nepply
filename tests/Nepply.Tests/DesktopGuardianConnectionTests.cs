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
    }
}
