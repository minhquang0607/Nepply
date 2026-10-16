using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Nepply.Services;
using Nepply.Views;

internal static class SecurityRegressionTests
{
    internal static void Run(string output,Action<string,bool> check)
    {
        var folder=Path.Combine(output,"security-regression");
        Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"fixture.lease");
        const string marker="restore-visible-v1";
        foreach(var encoding in new Encoding[]{new UTF8Encoding(false),new UTF8Encoding(true),Encoding.Unicode,Encoding.BigEndianUnicode,Encoding.UTF32})
        {
            File.WriteAllText(path,marker,encoding);
            check("Journal keeps legacy marker encoding "+encoding.WebName+"/"+encoding.GetPreamble().Length,DesktopVisibilityLease.HasRestoreMarker(path));
        }
        foreach(var text in new[]{"",marker[..^1],marker+"x",marker+"\n","restore-visible-v2"})
        {
            File.WriteAllText(path,text);
            check("Journal rejects malformed marker "+text.Length,!DesktopVisibilityLease.HasRestoreMarker(path));
        }
        File.WriteAllText(path,new string('x',4*1024*1024));
        long before=GC.GetAllocatedBytesForCurrentThread();
        bool restored=new DesktopVisibilityLease(new UntouchedDesktop(),path).Restore();
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        check("Oversize journal is rejected before large allocation",!restored && allocated<256*1024);
        check("Rejected journal remains available for recovery",File.Exists(path));
        foreach(int count in new[]{20,100,300,500,DesktopImageBudget.MaximumEntries})
            check("Bounded enumeration keeps complete inventory "+count,DesktopEntryReader.ReadBoundedEntries(Enumerable.Range(0,count).Select(i=>i.ToString())).Length==count);
        int visited=0;
        IEnumerable<string> Endless() {while(true) {visited++;yield return "fixture";}}
        bool capped=false;
        try {DesktopEntryReader.ReadBoundedEntries(Endless());} catch(InvalidDataException) {capped=true;}
        check("Enumeration aborts at budget plus one",capped && visited==DesktopImageBudget.MaximumEntries+1);
        var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(2,2,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[16],8);
        var cache=new Dictionary<int,System.Windows.Media.Imaging.BitmapSource>{{1,bitmap}};
        long remaining=16;
        check("Producer retains image fitting exact pixel budget",DesktopEntryReader.RetainImage(bitmap,cache,ref remaining)==bitmap && remaining==0);
        check("Producer rejects and releases cached image over budget",DesktopEntryReader.RetainImage(bitmap,cache,ref remaining)==null && cache.Count==0);
        VerifyDrag(check);
    }
    private static void VerifyDrag(Action<string,bool> check)
    {
        var source=new Border(); var target=new Border(); var panel=new StackPanel();
        panel.Children.Add(source); panel.Children.Add(target);
        var owner=new Window {Content=panel};
        var foreignTarget=new Border(); var otherPanel=new StackPanel(); otherPanel.Children.Add(foreignTarget);
        var other=new Window {Content=otherPanel};
        WidgetDrag.SetEnabled(source,true); WidgetDrag.SetEnabled(target,true); WidgetDrag.SetEnabled(foreignTarget,true);
        try
        {
            var data=new BrokenData();
            foreach(var method in new[]{"Over","Drop"})
            {
                var args=(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,
                    null,[data,DragDropKeyStates.None,DragDropEffects.Move,target,new Point()],null)!;
                args.RoutedEvent=method=="Over" ? UIElement.DragOverEvent : UIElement.DropEvent;
                typeof(WidgetDrag).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[target,args]);
                check("External widget "+method+" rejected without reading data",data.Calls==0 && args.Effects==DragDropEffects.None && args.Handled);
            }
            check("Widget drag starts an owner-local session",WidgetDrag.WithDragSession(source,()=>
            {
                check("Widget session accepts sibling",WidgetDrag.InternalSource(target)==source);
                check("Widget session rejects source and foreign window",WidgetDrag.InternalSource(source)==null && WidgetDrag.InternalSource(foreignTarget)==null);
                check("Widget session rejects nested drag",!WidgetDrag.WithDragSession(source,()=>throw new Exception("Unexpected nested drag")));
            }));
            check("Widget session clears after cancellation",WidgetDrag.InternalSource(target)==null);
            try {WidgetDrag.WithDragSession(source,()=>throw new InvalidOperationException("fixture"));}
            catch(InvalidOperationException) { }
            check("Widget session clears after exception",WidgetDrag.InternalSource(target)==null);
            WidgetDrag.WithDragSession(source,()=>
            {
                WidgetDrag.SetEnabled(source,false); WidgetDrag.SetEnabled(source,true);
                check("Edit off/on cannot revive stale drag",WidgetDrag.InternalSource(target)==null);
            });
            WidgetDrag.WithDragSession(source,()=>
            {
                source.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                check("Unloaded widget cancels active drag",WidgetDrag.InternalSource(target)==null);
            });
            int reordered=0;
            target.AddHandler(WidgetDrag.ReorderedEvent,new RoutedEventHandler((_,_)=>reordered++));
            WidgetDrag.WithDragSession(source,()=>
            {
                var args=(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,
                    null,[data,DragDropKeyStates.None,DragDropEffects.Move,target,new Point()],null)!;
                args.RoutedEvent=UIElement.DropEvent;
                typeof(WidgetDrag).GetMethod("Drop",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[target,args]);
                check("Internal drop reorders once without reading payload",panel.Children[1]==source && reordered==1 && data.Calls==0 && args.Effects==DragDropEffects.Move);
                check("Accepted drop consumes its session",WidgetDrag.InternalSource(target)==null);
            });
        }
        finally {owner.Close();other.Close();}
    }
    private sealed class UntouchedDesktop : IDesktopVisibility
    {
        public bool? IsVisible()=>throw new InvalidOperationException("Unexpected Desktop read");
        public bool SetVisible(bool visible)=>throw new InvalidOperationException("Unexpected Desktop write");
    }
    private sealed class BrokenData : IDataObject
    {
        internal int Calls;
        public object GetData(string format) {Calls++;throw new System.Runtime.InteropServices.COMException("Untrusted drag fixture");}
        public object GetData(string format,bool autoConvert)=>GetData(format);
        public object GetData(Type format)=>GetData(format.FullName!);
        public bool GetDataPresent(string format)=>true;
        public bool GetDataPresent(string format,bool autoConvert)=>true;
        public bool GetDataPresent(Type format)=>true;
        public string[] GetFormats()=>["Nepply.Widget"];
        public string[] GetFormats(bool autoConvert)=>GetFormats();
        public void SetData(object data)=>throw new NotSupportedException();
        public void SetData(string format,object data)=>throw new NotSupportedException();
        public void SetData(string format,object data,bool autoConvert)=>throw new NotSupportedException();
        public void SetData(Type format,object data)=>throw new NotSupportedException();
    }
}
