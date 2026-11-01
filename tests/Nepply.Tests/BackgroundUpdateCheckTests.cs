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
        Func<CancellationToken, Task<UpdateRelease?>> respond = _ => Task.FromResult<UpdateRelease?>(release);
        using var job = new BackgroundUpdateCheck(() => enabled, token => { requests++; return respond(token); }, r => notified.Add(r.Tag), () => now);

        check("New settings keep the daily update check off", !new AppSettings().AutoCheckUpdates);
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
        respond = _ => Task.FromException<UpdateRelease?>(new HttpRequestException("offline"));
        await job.TickAsync(default);
        check("A failed check stays silent", requests == 4 && notified.Count == 2);
        await job.TickAsync(default);
        check("A failed check waits for the next interval", requests == 4);

        now += BackgroundUpdateCheck.Interval;
        respond = _ => Task.FromException<UpdateRelease?>(new OperationCanceledException());
        await job.TickAsync(default);
        check("A request timeout counts as a failed check", requests == 5 && notified.Count == 2);

        now += BackgroundUpdateCheck.Interval;
        release = release with { Tag = "v9.0.2" };
        respond = _ => { enabled = false; return Task.FromResult<UpdateRelease?>(release); };
        await job.TickAsync(default);
        check("Turning the check off during a request drops the notice", requests == 6 && notified.Count == 2);

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
