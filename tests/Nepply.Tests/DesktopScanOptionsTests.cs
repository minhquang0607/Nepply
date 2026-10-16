using Nepply.Models;
using Nepply.Modules.Desktop;
using Nepply.Services;

internal static class DesktopScanOptionsTests
{
    internal static void Run(Action<string,bool> check)
    {
        var entered=new TaskCompletionSource<DesktopScanOptions>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen=new List<DesktopScanOptions>();
        using var service=new DesktopIconService(async (options,token)=>
        {
            seen.Add(options);
            if(seen.Count==1)
            {
                entered.SetResult(options);
                await release.Task.WaitAsync(token);
            }
            return new List<DesktopIcon>();
        });
        var running=service.ScanDesktopIconsAsync();
        try
        {
            var first=entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            check("Scan captures default options",first==new DesktopScanOptions());
            service.Configure(new DesktopScanOptions(true,false,false));
            check("In-flight scan options do not mutate with settings",first==new DesktopScanOptions());
            var pending=service.ScanDesktopIconsAsync();
            check("Options update retains coalesced scan task",ReferenceEquals(running,pending));
        }
        finally { release.TrySetResult(); }
        running.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        check("Follow-up scan captures new options",seen.Count==2 && seen[1]==new DesktopScanOptions(true,false,false));
        check("Distinct scans retain separate configuration revisions",!ReferenceEquals(seen[0],seen[1]) && seen[0]==new DesktopScanOptions());
        service.Configure(new DesktopScanOptions(true,false,false));
        service.ScanDesktopIconsAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        check("Equivalent configuration retains current revision",seen.Count==3 && ReferenceEquals(seen[1],seen[2]));
        bool rejected=false;
        try { service.Configure(null!); } catch(ArgumentNullException) { rejected=true; }
        check("Scan configuration rejects null before publication",rejected && service.ShowHiddenFiles && !service.ShowSystemIcons && !service.MergeEquivalentShortcuts);
        service.Dispose();
        service.Configure(new DesktopScanOptions());
        check("Disposed scanner ignores configuration publication",service.ShowHiddenFiles && !service.ShowSystemIcons && !service.MergeEquivalentShortcuts);
    }
}
