using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Nepply.Services;

// Package-only integration of the real deployment and Desktop service.
internal static class PackagedGuardianTests
{
    internal static int Run(string output,bool waitForRemoval=false)=>RunAsync(output,waitForRemoval).GetAwaiter().GetResult();
    private static async Task<int> RunAsync(string output,bool waitForRemoval)
    {
        if(!ProcessPackageIdentity.IsPackaged || Windows.ApplicationModel.Package.Current.Id.Name!="Nepply.Internal.Preview")return 2;
        var desktop=new ExplorerDesktopVisibility();
        bool? initial=desktop.IsVisible();
        string journal=DesktopVisibilityLease.JournalPath;
        string payload=Path.Combine(Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path,"DesktopGuardian","Nepply.DesktopGuardian.exe");
        bool hidden=false,restored=false,rescue=false;
        string? error=null;
        DesktopIconService? service=null;
        try
        {
            if(initial!=true || File.Exists(journal))throw new IOException("Desktop not available for isolated packaged QA.");
            service=new DesktopIconService();
            string? notice=await service.SetDesktopIconsHiddenAsync(true);
            hidden=notice is null && desktop.IsVisible()==false && File.Exists(journal);
            if(!hidden)throw new IOException(notice ?? "Packaged guarded hide failed.");
            if(waitForRemoval)
            {
                var helpers=Process.GetProcessesByName("Nepply.DesktopGuardian");
                try
                {
                    if(helpers.Length!=1)throw new IOException("Ambiguous QA guardian process.");
                    var helper=helpers[0];
                    string temporary=Path.Combine(output,"removal-ready.tmp");
                    File.WriteAllText(temporary,JsonSerializer.Serialize(new {
                        Initial=initial,Journal=journal,Payload=payload,Pid=helper.Id,Ticks=helper.StartTime.ToUniversalTime().Ticks
                    }));
                    File.Move(temporary,Path.Combine(output,"removal-ready.json"));
                }
                finally{foreach(var helper in helpers)helper.Dispose();}
                await Task.Delay(TimeSpan.FromSeconds(90)); // Restore normally if the harness never removes us.
            }
            notice=await service.SetDesktopIconsHiddenAsync(false);
            restored=notice is null && desktop.IsVisible()==true && !File.Exists(journal);
            if(!restored)throw new IOException(notice ?? "Packaged restoration verification failed.");
        }
        catch(Exception ex){error=ex.ToString();}
        finally
        {
            if(service is not null)
            {
                if(desktop.IsVisible()!=true || File.Exists(journal))rescue=true;
                try{service.Dispose();}
                catch(Exception ex){error=(error ?? "")+"\nDispose: "+ex;}
                if(desktop.IsVisible()!=true)
                {
                    rescue=true;
                    desktop.SetVisible(true);
                    if(desktop.IsVisible()==true)DesktopVisibilityLease.Create().Restore();
                }
            }
        }
        var processes=Process.GetProcessesByName("Nepply.DesktopGuardian");
        bool exited=processes.Length==0;
        foreach(var process in processes)process.Dispose();
        bool passed=hidden && restored && !rescue && exited && error is null;
        File.WriteAllText(Path.Combine(output,"packaged-guardian.json"),JsonSerializer.Serialize(new {
            Passed=passed,Initial=initial,Hidden=hidden,Restored=restored,RescueUsed=rescue,
            Final=desktop.IsVisible(),Journal=journal,JournalRemains=File.Exists(journal),Payload=payload,
            PayloadExists=File.Exists(payload),GuardianExited=exited,Error=error,
            Scope="Installed QA package, production DesktopIconService and guardian deployment, real Explorer normal hide/restore; not removal/update while hidden."
        },new JsonSerializerOptions{WriteIndented=true}));
        return passed ? 0 : 1;
    }

    internal static int VerifyAfterRemoval(string output)
    {
        if(ProcessPackageIdentity.IsPackaged)return 2;
        using var ready=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"removal-ready.json")));
        if(!ready.RootElement.GetProperty("Initial").GetBoolean())return 2;
        var desktop=new ExplorerDesktopVisibility();
        bool? observed=desktop.IsVisible();
        bool rescued=false;
        // Harness invokes only after owner and guardian have exited. A rescue is
        // recorded separately and must make the acceptance test fail.
        if(observed==false)rescued=desktop.SetVisible(true);
        File.WriteAllText(Path.Combine(output,"after-removal.json"),JsonSerializer.Serialize(new {
            RestoredBeforeRescue=observed==true,Observed=observed,RescueUsed=observed==false,RescueSucceeded=rescued,Final=desktop.IsVisible()
        }));
        return observed==true ? 0 : 1;
    }
}
