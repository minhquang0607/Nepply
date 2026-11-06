using System.Net.Http;
using Nepply.Models;
using Nepply.Modules.Settings;
using Nepply.Modules.Updates;

internal static class BackgroundUpdateCheckTests
{
    internal static void Run(Action<string, bool> check) => RunAsync(check).GetAwaiter().GetResult();

    private static async Task RunAsync(Action<string, bool> check)
    {
        var release = new UpdateRelease("v9.0.0", ReleaseVersion.Parse("9.0.0"), "", new Uri("https://example.invalid/setup.exe"), 1, new string('0', 64));
        var now = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        bool enabled = false;
        int requests = 0;
        var notified = new List<string>();
        bool online = true;
        int failureNotices = 0;
        Func<CancellationToken, Task<UpdateRelease?>> respond = _ => Task.FromResult<UpdateRelease?>(release);
        using var job = new BackgroundUpdateCheck(() => enabled, token => { requests++; return respond(token); }, r => notified.Add(r.Tag), () => now,
            () => online, () => failureNotices++);

        check("New settings turn the daily update check on", new AppSettings().AutoCheckUpdates);
        check("Turning the update check on needs no panel rebuild", SettingsChangeSet.Between(new AppSettings(), new AppSettings { AutoCheckUpdates = true }).CanPreviewWithoutRebuild);
        await job.TickAsync(default);
        check("Update check off sends no request", requests == 0 && notified.Count == 0);

        enabled = true;
        await job.TickAsync(default);
        check("Update check on reports a new version", requests == 1 && notified.SequenceEqual(["v9.0.0"]));

        now += TimeSpan.FromHours(23);
        await job.TickAsync(default);
        check("Update check runs at most once a day", requests == 1);

        now += TimeSpan.FromHours(1);
        await job.TickAsync(default);
        check("The next day checks again without repeating the notice", requests == 2 && notified.Count == 1);

        now += BackgroundUpdateCheck.Interval;
        release = release with { Tag = "v9.0.1" };
        await job.TickAsync(default);
        check("A newer version gets its own notice", requests == 3 && notified.SequenceEqual(["v9.0.0", "v9.0.1"]));

        now += BackgroundUpdateCheck.Interval;
        online = false;
        await job.TickAsync(default);
        check("Without a network nothing is sent", requests == 3);
        now += TimeSpan.FromHours(30);
        await job.TickAsync(default);
        check("A long offline stretch is not a failure and sends nothing", requests == 3 && failureNotices == 0);
        online = true;
        now += TimeSpan.FromHours(1);
        await job.TickAsync(default);
        check("The check runs at the first poll after the network returns", requests == 4);

        now += BackgroundUpdateCheck.Interval;
        respond = _ => Task.FromException<UpdateRelease?>(new HttpRequestException("unreachable"));
        await job.TickAsync(default);
        check("A first failure stays silent", requests == 5 && notified.Count == 2 && failureNotices == 0);
        now += BackgroundUpdateCheck.RetryDelay - TimeSpan.FromHours(1);
        await job.TickAsync(default);
        check("A failed check waits twelve hours before trying again", requests == 5);
        now += TimeSpan.FromHours(1);
        respond = _ => Task.FromException<UpdateRelease?>(new OperationCanceledException());
        await job.TickAsync(default);
        check("A request timeout counts as a failure", requests == 6);
        check("The second failure in a row is reported once", failureNotices == 1);
        now += BackgroundUpdateCheck.RetryDelay;
        await job.TickAsync(default);
        check("Further failures keep retrying without repeating the report", requests == 7 && failureNotices == 1);

        now += BackgroundUpdateCheck.RetryDelay;
        respond = _ => Task.FromResult<UpdateRelease?>(release);
        await job.TickAsync(default);
        check("A success returns to the daily rhythm", requests == 8);
        now += BackgroundUpdateCheck.RetryDelay;
        await job.TickAsync(default);
        check("After a success the next check waits a full day", requests == 8);

        respond = _ => Task.FromException<UpdateRelease?>(new HttpRequestException("unreachable"));
        now += BackgroundUpdateCheck.RetryDelay;
        await job.TickAsync(default);
        now += BackgroundUpdateCheck.RetryDelay;
        await job.TickAsync(default);
        check("A success resets the count, so a new pair of failures reports again", requests == 10 && failureNotices == 2);

        now += BackgroundUpdateCheck.RetryDelay;
        release = release with { Tag = "v9.0.2" };
        respond = _ => { enabled = false; return Task.FromResult<UpdateRelease?>(release); };
        await job.TickAsync(default);
        check("Turning the check off during a request drops the notice", requests == 11 && notified.Count == 2);

        now += BackgroundUpdateCheck.Interval;
        enabled = true;
        respond = _ => Task.FromException<UpdateRelease?>(new HttpRequestException("unreachable"));
        await job.TickAsync(default);
        now += BackgroundUpdateCheck.RetryDelay;
        respond = _ => { enabled = false; return Task.FromException<UpdateRelease?>(new HttpRequestException("unreachable")); };
        await job.TickAsync(default);
        check("No failure report once the check was turned off", requests == 13 && failureNotices == 2);
        enabled = true;

        now += BackgroundUpdateCheck.Interval;
        enabled = true;
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        respond = token => Task.FromCanceled<UpdateRelease?>(token);
        bool cancelled = false;
        try { await job.TickAsync(stop.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        check("App exit ends a running check", cancelled);
    }
}
