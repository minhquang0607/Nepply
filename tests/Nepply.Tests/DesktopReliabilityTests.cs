using System.IO;
using Nepply.Models;
using Nepply.Services;

internal static class DesktopReliabilityTests
{
    internal static void Run(string root,Action<string,bool> check)
    {
        var stable = new DesktopIcon { Name="App", Path=@"C:\Desktop\App.lnk", SourcePaths=[@"C:\Desktop\App.lnk"] };
        var replacement = new DesktopIcon { Name=stable.Name, Path=stable.Path, SourcePaths=[stable.Path] };
        check("Unchanged Desktop item retains reference",ReferenceEquals(DesktopSnapshot.Reconcile([stable],[replacement])[0],stable));
        replacement.Name="New name";
        check("Changed Desktop name is published",ReferenceEquals(DesktopSnapshot.Reconcile([stable],[replacement])[0],replacement));
        check("Mouse press survives equivalent scan replacement",Nepply.Views.IconPanel.SameActionTarget(replacement,stable));
        check("Mouse release never launches a different target",!Nepply.Views.IconPanel.SameActionTarget(new DesktopIcon {Path="other"},stable));
        check("Mouse release without press is ignored",!Nepply.Views.IconPanel.SameActionTarget(stable,null));
        replacement.Name=stable.Name; replacement.SourcePaths=[stable.Path,"second"];
        check("Grouped source changes invalidate snapshot item",ReferenceEquals(DesktopSnapshot.Reconcile([stable],[replacement])[0],replacement));
        System.Windows.Media.Imaging.BitmapSource Thumbnail(byte channel)
        {
            var image=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[]{channel,0,0,255},4);
            image.Freeze(); return image;
        }
        replacement.SourcePaths=stable.SourcePaths;
        stable.Icon=Thumbnail(20); replacement.Icon=Thumbnail(20);
        check("Identical frozen pixels reuse existing icon",ReferenceEquals(DesktopSnapshot.Reconcile([stable],[replacement])[0],stable));
        replacement.Icon=Thumbnail(21);
        check("Changed icon pixels are not hidden by identity reuse",ReferenceEquals(DesktopSnapshot.Reconcile([stable],[replacement])[0],replacement));
        int publications=0;
        using(var service=new DesktopIconService(_=>Task.FromResult(new List<DesktopIcon> {new() {Name="App",Path="test"}})))
        {
            service.IconsChanged+=()=>publications++;
            service.ScanDesktopIconsAsync().GetAwaiter().GetResult();
            service.ScanDesktopIconsAsync().GetAwaiter().GetResult();
            check("Identical repeated scan publishes only once",publications==1 && service.HasScanned && !service.ScanState.IsScanning);
        }
        int restored=0,released=0;
        check("Recovery refuses live owner without touching Desktop",DesktopRecovery.Run(()=>false,()=>{restored++;return true;},()=>released++)==DesktopRecoveryResult.AppRunning && restored==0 && released==0);
        check("Recovery releases acquired ownership after success",DesktopRecovery.Run(()=>true,()=>{restored++;return true;},()=>released++)==DesktopRecoveryResult.Restored && restored==1 && released==1);
        check("Recovery reports unavailable Desktop",DesktopRecovery.Run(()=>true,()=>false,()=>released++)==DesktopRecoveryResult.Unavailable && released==2);
        check("Recovery exception still releases ownership",DesktopRecovery.Run(()=>true,()=>throw new IOException("fixture"),()=>released++)==DesktopRecoveryResult.Failed && released==3);
        string empty=Path.Combine(root,"empty-source"); Directory.CreateDirectory(empty);
        check("Complete empty source is distinct from failure",DesktopEntryReader.ReadFolderEntries(empty).Length==0);
        bool failed=false;
        try { DesktopEntryReader.ReadFolderEntries(Path.Combine(root,"missing-source")); }
        catch(DirectoryNotFoundException) { failed=true; }
        check("Unavailable source must not masquerade as empty Desktop",failed);
        check("Entry deleted after enumeration is skipped",DesktopEntryReader.ReadEntryAttributes(Path.Combine(empty,"deleted.txt"),empty)==null);
        bool rootLost=false;
        try { DesktopEntryReader.ReadEntryAttributes(Path.Combine(root,"missing-source","deleted.txt"),Path.Combine(root,"missing-source")); }
        catch(DirectoryNotFoundException) { rootLost=true; }
        catch(FileNotFoundException) { rootLost=true; }
        check("Entry race never hides unavailable root",rootLost);
        var entries=new[] {new DesktopIcon {Name="App",Path=@"C:\User\App.lnk"},new DesktopIcon {Name="App",Path=@"C:\Public\App.lnk"}};
        foreach(var entry in entries) entry.GroupIdentity="same";
        var grouped=Nepply.Modules.Desktop.DesktopRawCatalog.Group(entries,true);
        check("Grouping preserves all physical shortcut paths",grouped.Count==1 && grouped[0].SourcePaths.Length==2);
        check("Grouped tooltip explains which item receives context commands",grouped[0].SourceDescription.Key=="GroupedShortcutDescription"
            && Equals(grouped[0].SourceDescription.Arguments[1],grouped[0].Path)
            && grouped[0].SourceDescription.Arguments[2] is string paths && paths.Contains(entries[0].Path) && paths.Contains(entries[1].Path));
        check("Disabling grouping restores distinct physical items",Nepply.Modules.Desktop.DesktopRawCatalog.Group(entries,false).Count==2);
        foreach(var entry in entries) entry.GroupIdentity=entry.Path;
        check("Different shortcut behavior remains separate",Nepply.Modules.Desktop.DesktopRawCatalog.Group(entries,true).Count==2);
        var wire=new DesktopScanProcess.Reply(1,[new("App",entries[0].Path,false,null,[entries[0].Path,entries[1].Path])]);
        var decoded=DesktopScanProcess.DecodeReply(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(wire));
        check("Desktop helper preserves grouped source paths",decoded[0].SourcePaths.Length==2);
        var broken=new DesktopScanProcess.Reply(1,[new("App",entries[0].Path,false,null,[""])]);
        failed=false;
        try { DesktopScanProcess.DecodeReply(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(broken)); }
        catch(InvalidDataException) { failed=true; }
        check("Desktop helper rejects invalid grouped source paths",failed);
    }
}
