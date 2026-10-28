using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Nepply;

internal static class TrayRegistrationTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Identifier { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { public int Left, Top, Right, Bottom; }
    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref Identifier identifier, out Rectangle rectangle);
    internal static int Run(string output, bool explorerRecovery = false)
    {
        var app = new App(testHost: true); app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new MainWindow(preview: true);
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        TaskbarIcon? tray = null; Action? detach = null;
        bool initial = false, removed = false, recreated = false, disposed = false;
        var identifier = new Identifier { Size = (uint)Marshal.SizeOf<Identifier>() };
        try
        {
            typeof(App).GetMethod("SetupTray", privateInstance)!.Invoke(app, [window]);
            tray = (TaskbarIcon)typeof(App).GetField("_tray", privateInstance)!.GetValue(app)!;
            detach = (Action?)typeof(App).GetField("_detachTrayLanguage", privateInstance)!.GetValue(app);
            var data = typeof(TaskbarIcon).GetField("iconData", privateInstance)!.GetValue(tray)!;
            identifier.Window = (IntPtr)data.GetType().GetField("WindowHandle")!.GetValue(data)!;
            identifier.Id = Convert.ToUInt32(data.GetType().GetField("TaskbarIconId")!.GetValue(data));
            Pump(); initial = Shell_NotifyIconGetRect(ref identifier, out _) == 0;
            if (explorerRecovery)
            {
                if (!initial) throw new InvalidOperationException("Initial tray registration unavailable.");
                File.WriteAllText(Path.Combine(output, "ready-for-explorer-restart"), Environment.ProcessId.ToString());
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(output, "explorer-restarted")) && deadline.Elapsed < TimeSpan.FromMinutes(3)) Pump();
                if (!File.Exists(Path.Combine(output, "explorer-restarted"))) throw new TimeoutException("Missing restart acknowledgment.");
            }
            else
            {
                typeof(TaskbarIcon).GetMethod("RemoveTaskbarIcon", privateInstance)!.Invoke(tray, null);
                Pump(); removed = Shell_NotifyIconGetRect(ref identifier, out _) != 0;
                typeof(TaskbarIcon).GetMethod("OnTaskbarCreated", privateInstance)!.Invoke(tray, null);
            }
            Pump(); recreated = Shell_NotifyIconGetRect(ref identifier, out _) == 0;
        }
        finally
        {
            detach?.Invoke(); tray?.Dispose();
            if (identifier.Window != IntPtr.Zero) disposed = Shell_NotifyIconGetRect(ref identifier, out _) != 0;
            (typeof(App).GetField("_icon", privateInstance)!.GetValue(app) as IDisposable)?.Dispose();
            window.Cleanup(); window.Close(); app.MainWindow = null;
        }
        bool passed = initial && (explorerRecovery || removed) && recreated && disposed;
        File.WriteAllText(Path.Combine(output, "tray-registration.json"), JsonSerializer.Serialize(new
        {
            Passed = passed,
            ExplorerRecovery = explorerRecovery,
            Initial = initial,
            Removed = explorerRecovery ? (bool?)null : removed,
            Recreated = recreated,
            Disposed = disposed,
            Scope = "Production SetupTray with isolated preview settings; real Windows icon query and disposal. " + (explorerRecovery ? "External Explorer restart; no direct recovery callback, acknowledgment required." : "Direct OnTaskbarCreated after removal, not a broadcast-delivery test.") + " No mouse interaction."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Tray initial={initial}; removed={removed}; recreated={recreated}; disposed={disposed}");
        return passed ? 0 : 1;
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        EventHandler handler = (_, _) => frame.Continue = false;
        timer.Tick += handler; timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); timer.Tick -= handler; }
    }
}
