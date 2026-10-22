using System.IO;
using System.Text;
using System.Text.Json;
using Nepply.Services;

internal static class DesktopScanReplyTests
{
    internal static void Run(Action<string,bool> check)
    {
        bool canceledBeforeStart=false;
        try
        {
            DesktopScanProcess.ReadAsync(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".exe"),false,false,new CancellationToken(true))
                .GetAwaiter().GetResult();
        }
        catch(OperationCanceledException) {canceledBeforeStart=true;}
        catch(System.ComponentModel.Win32Exception) { }
        check("Pre-canceled Desktop scan does not attempt helper startup",canceledBeforeStart);
        foreach(var json in new[] {"null", "{\"Version\":1}", "{\"Version\":2,\"Entries\":[]}",
            "{\"Version\":1,\"Entries\":[null]}",
            "{\"Version\":1,\"Entries\":[{\"Name\":\"File\",\"Path\":\" \"}]}",
            "{\"Version\":1,\"Entries\":[{\"Path\":\"C:\\\\Desktop\\\\File\"}]}" })
        {
            bool rejected=false;
            try {DesktopScanProcess.DecodeReply(Encoding.UTF8.GetBytes(json));}
            catch(InvalidDataException) {rejected=true;}
            check("Incomplete Desktop reply rejected: "+json,rejected);
        }
        var entries=new[] {
            new DesktopScanProcess.Entry("Same name",@"C:\Desktop\A.lnk",false,[0,1,2,3]),
            new DesktopScanProcess.Entry("Same name",@"C:\Public\Desktop\A.lnk",false,null),
            new DesktopScanProcess.Entry("Control Panel","::{26EE0668-A00A-44D7-9371-BEB064C98683}",true,[]) };
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new DesktopScanProcess.Reply(1,entries));
        var icons=DesktopScanProcess.DecodeReply(bytes);
        check("Bad thumbnail does not remove Desktop items",icons.Count==3 && icons[0].Icon==null && icons[0].Path==entries[0].Path);
        check("Reply keeps distinct same-name shortcuts",icons[0].Name==icons[1].Name && icons[0].Path!=icons[1].Path);
        check("Reply keeps system icon identity without thumbnail",icons[2].IsSystemIcon && icons[2].Path==entries[2].Path);
        bool canceled=false;
        try {DesktopScanProcess.DecodeReply(bytes,new CancellationToken(true));}
        catch(OperationCanceledException) {canceled=true;}
        check("Canceled scan does not decode thumbnails",canceled);
        check("Empty valid Desktop reply is accepted",DesktopScanProcess.DecodeReply(Encoding.UTF8.GetBytes("{\"Version\":1,\"Entries\":[]}")).Count==0);
        bool failureReported=false;
        try {DesktopScanProcess.DecodeReply(DesktopScanProcess.EncodeFailure("fixture reader error"));}
        catch(IOException ex) {failureReported=ex.Message.Contains("fixture reader error");}
        check("Reader failure is distinct from empty successful Desktop",failureReported);
        bool boundedFailure=false;
        try {DesktopScanProcess.DecodeReply(DesktopScanProcess.EncodeFailure(new string('x',AppLogger.MaximumEntryCharacters+1000)));}
        catch(IOException ex) {boundedFailure=ex.Message.Length<=AppLogger.MaximumEntryCharacters+30 && ex.Message.Contains("diagnostic truncated");}
        check("Reader failure diagnostic is bounded",boundedFailure);
        bool mixedFailureRejected=false;
        try {DesktopScanProcess.DecodeReply(JsonSerializer.SerializeToUtf8Bytes(new DesktopScanProcess.Reply(1,entries,"failure")));}
        catch(InvalidDataException) {mixedFailureRejected=true;}
        check("Reader cannot publish items alongside failed scan",mixedFailureRejected);
        var hugeHeader=new byte[33];
        new byte[]{137,80,78,71,13,10,26,10}.CopyTo(hugeHeader,0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(hugeHeader.AsSpan(8),13);
        "IHDR"u8.CopyTo(hugeHeader.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(hugeHeader.AsSpan(16),100000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(hugeHeader.AsSpan(20),100000);
        hugeHeader[24]=8;
        bool oversized=false;
        try { DesktopImageBudget.ValidatePng(hugeHeader); } catch(InvalidDataException) { oversized=true; }
        check("Oversized PNG rejected before decoding pixels",oversized);
        var oversizedReply=new DesktopScanProcess.Reply(1,[new("Large icon","test",false,hugeHeader)]);
        check("Oversized icon keeps item identity",DesktopScanProcess.DecodeReply(JsonSerializer.SerializeToUtf8Bytes(oversizedReply)) is {Count:1} kept && kept[0].Icon==null);
        using(var limited=new LimitedMemoryStream(8))
        {
            limited.Write(new byte[8]); bool over=false;
            try { limited.WriteByte(1); } catch(InvalidDataException) { over=true; }
            check("Producer stops at byte budget without writing overflow",over && limited.Length==8);
        }
        var encoded=DesktopScanProcess.EncodeReply([new Nepply.Models.DesktopIcon {Name="No image",Path="test"}]);
        check("Streaming producer preserves protocol and image-free identity",DesktopScanProcess.DecodeReply(encoded)[0].Name=="No image");
        var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(48,48,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[48*48*4],48*4);
        bitmap.Freeze();
        var imageReply=DesktopScanProcess.EncodeReply([new Nepply.Models.DesktopIcon {Name="Image",Path="image",Icon=bitmap}]);
        var decodedImage=DesktopScanProcess.DecodeReply(imageReply)[0].Icon as System.Windows.Media.Imaging.BitmapSource;
        check("Bounded producer transfers a valid frozen PNG",decodedImage is {PixelWidth:48,PixelHeight:48,IsFrozen:true});
        var maximum=System.Windows.Media.Imaging.BitmapSource.Create(512,512,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[512*512*4],512*4);
        maximum.Freeze();
        var boundedIcons=Enumerable.Range(0,34).Select(i=>new Nepply.Models.DesktopIcon {Name="Budget "+i,Path="budget-"+i,Icon=maximum});
        var boundedReply=DesktopScanProcess.DecodeReply(DesktopScanProcess.EncodeReply(boundedIcons));
        check("Aggregate bitmap budget preserves all items with bounded image count",boundedReply.Count==34 && boundedReply.Count(i=>i.Icon!=null)==32);
        check("Shell menu handles menu draw messages only",Nepply.Helpers.ShellMenuSession.IsMenuMessage(0x2B,IntPtr.Zero)
            && !Nepply.Helpers.ShellMenuSession.IsMenuMessage(0x2B,new IntPtr(3))
            && Nepply.Helpers.ShellMenuSession.IsMenuMessage(0x120,IntPtr.Zero)
            && !Nepply.Helpers.ShellMenuSession.IsMenuMessage(0x200,IntPtr.Zero));
        PopupTracking(check);
    }
    private static void PopupTracking(Action<string,bool> check)
    {
        var owner=new IntPtr(0x1234);
        var calls=new List<string>();
        int selected=Nepply.Helpers.PopupMenuTracking.Track(owner,()=>{calls.Add("track");return 7;},
            window=>calls.Add("foreground:"+window),window=>calls.Add("null:"+window));
        check("Popup owner becomes foreground before tracking and gets WM_NULL after",selected==7
            && calls.SequenceEqual(["foreground:"+owner,"track","null:"+owner]));
        calls.Clear();
        bool threw=false;
        try { Nepply.Helpers.PopupMenuTracking.Track(owner,()=>throw new InvalidOperationException("fixture"),_=>calls.Add("foreground"),_=>calls.Add("null")); }
        catch(InvalidOperationException) { threw=true; }
        check("Popup tracking still posts WM_NULL when tracking fails",threw && calls.SequenceEqual(["foreground","null"]));
    }
}
