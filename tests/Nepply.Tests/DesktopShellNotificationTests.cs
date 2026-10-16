using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Nepply.Services;

internal static class DesktopShellNotificationTests
{
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]
    private static extern void SHChangeNotify(int events,uint flags,[MarshalAs(UnmanagedType.LPWStr)] string first,[MarshalAs(UnmanagedType.LPWStr)] string? second);
    internal static int Run(string output)
    {
        var app=new Nepply.App(testHost:true);app.InitializeComponent();
        var folder=Path.Combine(output,"shell-notification-fixture");Directory.CreateDirectory(folder);
        var window=new Window {Width=100,Height=100,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
        var received=new List<string[]?>();
        var lines=new List<string>();int failures=0;
        void Check(string name,bool result) {lines.Add((result ? "PASS " : "FAIL ")+name);if(!result)failures++;}
        DesktopShellNotifications? subscription=null;
        try
        {
            new WindowInteropHelper(window).EnsureHandle();
            subscription=new DesktopShellNotifications(window,paths=>received.Add(paths),folder);
            string first=Path.Combine(folder,"first.txt"),second=Path.Combine(folder,"đổi-日本語.txt");
            File.WriteAllText(first,"Owned QA fixture");
            SHChangeNotify(2,0x2005,first,null);
            Check("Native Shell create event yields physical path",PumpUntil(()=>received.Any(p=>p?.Contains(first,StringComparer.OrdinalIgnoreCase)==true)));
            received.Clear();File.Move(first,second);SHChangeNotify(1,0x2005,first,second);
            Check("Native Shell rename event copies both Unicode paths",PumpUntil(()=>received.Any(p=>p?.Length==2
                && string.Equals(p[0],first,StringComparison.OrdinalIgnoreCase) && string.Equals(p[1],second,StringComparison.OrdinalIgnoreCase))));
            received.Clear();SHChangeNotify(0x1000,0x2005,folder,null);
            Check("Native Shell directory invalidation requests full repair",PumpUntil(()=>received.Any(p=>p==null)));
            subscription.Dispose();subscription=null;received.Clear();SHChangeNotify(0x2000,0x2005,second,null);
            PumpUntil(()=>false,250);
            Check("Disposed native Shell subscription ignores later delivery",received.Count==0);
        }
        catch(Exception ex) {Check("Native Shell fixture: "+ex,false);}
        finally {subscription?.Dispose();window.Close();}
        lines.Add($"RESULT: {lines.Count-failures} passed; {failures} failed. Real Shell notifications on an owned artifact folder; no user Desktop edits.");
        File.WriteAllLines(Path.Combine(output,"verification.txt"),lines);
        foreach(var line in lines) Console.WriteLine(line);
        return failures==0 ? 0 : 1;
    }
    private static bool PumpUntil(Func<bool> ready,int timeout=4000)
    {
        var clock=Stopwatch.StartNew();
        while(!ready() && clock.ElapsedMilliseconds<timeout)
        {
            var frame=new DispatcherFrame();
            var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
            timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
        }
        return ready();
    }
}
