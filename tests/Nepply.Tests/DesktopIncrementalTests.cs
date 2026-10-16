using System.IO;
using Nepply.Models;
using Nepply.Modules.Desktop;
using Nepply.Services;

internal static class DesktopIncrementalTests
{
    private const string Root=@"C:\FixtureDesktop", Public=@"C:\FixturePublic";
    private static DesktopIcon Item(string path,string? group=null) => new() {Path=path,Name=Path.GetFileNameWithoutExtension(path),GroupIdentity=group ?? path};
    private static void Wait(Task task)=>task.WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult();
    internal static void Run(Action<string,bool> check)
    {
        var batch=new DesktopChangeBatch();
        batch.Add([Root+@"\old.txt",Root+@"\OLD.txt"]);
        check("Dirty batch deduplicates paths but preserves new case spelling",batch.Take() is [var path] && path.EndsWith("OLD.txt"));
        check("Taking batch releases pending state",!batch.HasChanges);
        batch.Add(Enumerable.Range(0,513).Select(i=>Root+"\\"+i));
        check("Dirty batch over 512 paths promotes to full repair",batch.HasChanges && batch.Take()==null);
        batch.Add(Enumerable.Range(0,100).Select(i=>Root+"\\"+new string('a',1000)+i));
        check("Dirty batch bounds aggregate path memory",batch.Take()==null);
        batch.Add(["relative.txt"]);
        check("Unknown path requests full repair",batch.Take()==null);
        batch.Add([Root+@"\a.txt"]);batch.Full();batch.Add([Root+@"\b.txt"]);
        check("Full repair dominates file notifications",batch.Take()==null);
        check("Root filter excludes siblings, nested files and root itself",
            DesktopChangeBatch.IsDirectChild(Root+@"\a.txt",[Root]) && !DesktopChangeBatch.IsDirectChild(Root+@"2\a.txt",[Root])
            && !DesktopChangeBatch.IsDirectChild(Root+@"\nested\a.txt",[Root]) && !DesktopChangeBatch.IsDirectChild(Root,[Root]));
        foreach(int code in new[]{1,0x20000}) check("Shell rename retains both identities: "+code,DesktopShellNotifications.PathCountForEvent(code)==2);
        foreach(int code in new[]{2,4,8,16,0x800,0x2000}) check("Shell file event supports partial refresh: "+code,DesktopShellNotifications.PathCountForEvent(code)==1);
        check("Shell directory/association/unknown events retain full repair",DesktopShellNotifications.PathCountForEvent(0x1000)==0
            && DesktopShellNotifications.PathCountForEvent(0x8000000)==0 && DesktopShellNotifications.PathCountForEvent(0)==0);
        check("Shell interrupt flag preserves file classification",DesktopShellNotifications.PathCountForEvent(unchecked((int)0x80000002))==1);
        var request=DesktopScanProcess.EncodeRequest([Root+@"\日本語.txt",Root+@"\đổi tên.txt"]);
        check("Partial scan wire preserves Unicode identity",DesktopScanProcess.DecodeRequest(request)!.SequenceEqual([Root+@"\日本語.txt",Root+@"\đổi tên.txt"]));
        check("Full scan has explicit null path batch",DesktopScanProcess.DecodeRequest(DesktopScanProcess.EncodeRequest(null))==null);
        foreach(var invalid in new[]{new[]{"relative"},Enumerable.Range(0,513).Select(i=>Root+"\\"+i).ToArray(),new[]{new string('x',32769)}})
        {
            bool rejected=false;try {DesktopScanProcess.EncodeRequest(invalid);}catch(InvalidDataException){rejected=true;}
            check("Desktop wire rejects unsafe or oversized batch",rejected);
        }
        foreach(int count in new[]{20,100,300,500}) Membership(check,count);
        ConcurrentRequests(check);
        StaleRevision(check);
        ImageBudget(check);
    }
    private static void Membership(Action<string,bool> check,int count)
    {
        var disk=Enumerable.Range(0,count).Select(i=>Item(Root+$"\\Item{i:D3}.txt")).ToDictionary(i=>i.Path,StringComparer.OrdinalIgnoreCase);
        var requests=new List<string[]?>();
        bool fail=false;
        using var service=new DesktopIconService((options,paths,token)=>
        {
            requests.Add(paths);
            if(fail) {fail=false;throw new IOException("Fixture inaccessible source");}
            return Task.FromResult(paths==null ? disk.Values.ToList() : paths.Where(disk.ContainsKey).Select(p=>disk[p]).ToList());
        },()=>[Root,Public]);
        Wait(service.ScanDesktopIconsAsync());
        var untouched=service.GetIcons().Single(i=>i.Path==Root+@"\Item001.txt");
        var changed=Root+@"\Item000.txt";disk[changed]=new DesktopIcon {Path=changed,Name="Changed",GroupIdentity=changed};
        Wait(service.ScanDesktopChangesAsync([changed,changed]));
        check($"{count} items: one file reads one entry, preserving unrelated model",requests[^1] is [var only] && only==changed
            && service.GetIcons().Count==count && ReferenceEquals(untouched,service.GetIcons().Single(i=>i.Path==untouched.Path)));
        string renamed=Root+@"\đổi 日本語.txt";disk.Remove(changed);disk[renamed]=Item(renamed);
        Wait(service.ScanDesktopChangesAsync([changed,renamed]));
        check($"{count} items: rename removes old and adds Unicode identity",requests[^1]?.Length==2 && service.GetIcons().Count==count
            && !service.GetIcons().Any(i=>i.Path==changed) && service.GetIcons().Any(i=>i.Path==renamed));
        var a=Item(Root+@"\App.lnk","same-destination");var b=Item(Public+@"\App.lnk","same-destination");
        disk[a.Path]=a;disk[b.Path]=b;Wait(service.ScanDesktopChangesAsync([a.Path,b.Path]));
        check($"{count} items: partial additions merge User/Public shortcuts",service.GetIcons().Single(i=>i.Name=="App").SourcePaths.Length==2
            && a.SourcePaths.Length==0 && b.SourcePaths.Length==0);
        disk.Remove(a.Path);Wait(service.ScanDesktopChangesAsync([a.Path]));
        check($"{count} items: removing representative preserves surviving shortcut",service.GetIcons().Single(i=>i.Name=="App").Path==b.Path
            && service.GetIcons().Single(i=>i.Name=="App").SourcePaths.SequenceEqual([b.Path]));
        disk[a.Path]=a;Wait(service.ScanDesktopChangesAsync([a.Path]));
        disk[b.Path]=Item(b.Path,"other-destination");Wait(service.ScanDesktopChangesAsync([b.Path]));
        check($"{count} items: changing target splits previously merged shortcut",service.GetIcons().Count(i=>i.Name=="App")==2);
        var last=service.GetIcons();fail=true;Wait(service.ScanDesktopChangesAsync([b.Path]));
        check($"{count} items: failed partial scan preserves complete UI snapshot",ReferenceEquals(last,service.GetIcons()) && service.ScanState.Error!=null);
        Wait(service.ScanDesktopChangesAsync([b.Path]));
        check($"{count} items: next request repairs invalid baseline with full scan",requests[^1]==null && service.ScanState.Error==null);
        service.Configure(new(false,true,false));Wait(service.ScanDesktopChangesAsync([b.Path]));
        check($"{count} items: options revision forces full baseline",requests[^1]==null);
        Wait(service.ScanDesktopChangesAsync([Root]));
        check($"{count} items: root invalidation requests full scan",requests[^1]==null);
        bool rejected=false;
        try {DesktopRawCatalog.Merge([], [Item(@"C:\Outside\file")],[a.Path]);}catch(InvalidDataException){rejected=true;}
        check($"{count} items: partial response cannot inject unrelated membership",rejected);
    }
    private static void ConcurrentRequests(Action<string,bool> check)
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen=new List<string[]?>();
        using var service=new DesktopIconService(async (options,paths,token)=>
        {
            seen.Add(paths);
            if(seen.Count==2) {entered.SetResult();await release.Task.WaitAsync(token);}
            return paths==null ? [Item(Root+@"\a.txt")] : paths.Select(p=>Item(p)).ToList();
        },()=>[Root]);
        Wait(service.ScanDesktopIconsAsync());
        var running=service.ScanDesktopChangesAsync([Root+@"\a.txt"]);
        try
        {
            Wait(entered.Task);
            for(int i=0;i<100;i++) service.ScanDesktopChangesAsync([Root+@"\b.txt"]);
            check("In-flight change bursts share one worker",ReferenceEquals(running,service.ScanDesktopChangesAsync([Root+@"\b.txt"])) && seen.Count==2);
        }
        finally {release.TrySetResult();}
        Wait(running);
        check("Changes arriving during scan survive as one later batch",seen.Count==3 && seen[2] is [var path] && path==Root+@"\b.txt" && service.GetIcons().Count==2);
        service.RestorePreviewCatalog(new(),[],true);
        Wait(service.ScanDesktopChangesAsync([Root+@"\b.txt"]));
        check("Preview rollback invalidates raw cache before incremental updates",seen[^1]==null);
        service.Dispose();int before=seen.Count;Wait(service.ScanDesktopChangesAsync([Root+@"\c.txt"]));
        check("Shutdown rejects later partial requests",seen.Count==before);
    }
    private static void StaleRevision(Action<string,bool> check)
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int scans=0;
        string currentRoot=Root;
        var requests=new List<string[]?>();
        using var service=new DesktopIconService(async (options,paths,token)=>
        {
            int current=++scans;requests.Add(paths);
            if(current==2) {entered.SetResult();await release.Task.WaitAsync(token);}
            return [new DesktopIcon {Name=current==2 ? "obsolete" : "current",Path=currentRoot+@"\a.txt",GroupIdentity="same"}];
        },()=>[currentRoot]);
        Wait(service.ScanDesktopIconsAsync());
        var before=service.GetIcons();var published=new List<string>();
        service.IconsChanged+=()=>published.Add(service.GetIcons().First().Name);
        var running=service.ScanDesktopChangesAsync([Root+@"\a.txt"]);
        try {Wait(entered.Task);service.RestorePreviewCatalog(new(),before,true);}
        finally {release.TrySetResult();}
        Wait(running);
        check("Same-options preview rollback rejects obsolete partial reply",scans==3 && !published.Contains("obsolete") && requests[2]==null);
        currentRoot=Public;Wait(service.ScanDesktopChangesAsync([Public+@"\a.txt"]));
        check("Desktop root change invalidates raw baseline",requests[^1]==null && service.GetIcons().Single().Path.StartsWith(Public));
        var blocked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shutdown=new DesktopIconService(async (options,paths,token)=>
        {
            blocked.SetResult();await Task.Delay(Timeout.Infinite,token);return [];
        },()=>[Root]);
        var shutdownScan=shutdown.ScanDesktopIconsAsync();Wait(blocked.Task);shutdown.Dispose();Wait(shutdownScan);
        check("Shutdown cancels pending raw scan without publication",!shutdown.HasScanned && shutdown.GetIcons().Count==0);
    }
    private static void ImageBudget(Action<string,bool> check)
    {
        var image=System.Windows.Media.Imaging.BitmapSource.Create(512,512,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[512*512*4],512*4);image.Freeze();
        var raw=Enumerable.Range(0,33).Select(i=>new DesktopIcon {Path=Root+"\\"+i,Name=i.ToString(),Icon=image,GroupIdentity=i.ToString()}).ToList();
        var previous=raw[^1];DesktopRawCatalog.BoundImages(raw);
        check("Incremental raw cache preserves aggregate decoded-image budget",raw.Count(i=>i.Icon!=null)==32 && raw.Count==33
            && previous.Icon!=null && raw[^1].Icon==null && raw[^1].GroupIdentity==previous.GroupIdentity);
    }
}
