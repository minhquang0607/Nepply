using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;
using Nepply.Modules.Appearance;

internal static class MotionPolicyTests
{
    internal static void Run(Action<string,bool> check,Action<int> pump)
    {
        var meter=new ProgressBar {Minimum=0,Maximum=100,Width=120,Height=8};
        var host=new Window {Content=meter,Width=180,Height=90,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            host.Show();pump(20);
            var settings=new AppSettings {Motion=MotionMode.Quick};
            ThemeService.Apply(settings);
            check("Quick motion shares shorter duration",MotionPolicy.Duration(200).TotalMilliseconds==130);
            MeterMotion.SetTarget(meter,80);
            // Completion is dispatched on a render tick, which can be delayed on busy CI.
            // Assert eventual cleanup with a deadline rather than one fixed sleep.
            var deadline=System.Diagnostics.Stopwatch.StartNew();
            while((meter.HasAnimatedProperties || meter.Value!=80) && deadline.ElapsedMilliseconds<2000) pump(20);
            check("Meter releases completed animation clock",!meter.HasAnimatedProperties && meter.Value==80);
            MeterMotion.SetTarget(meter,30);host.Hide();
            check("Hidden meter stops immediately at latest sample",!meter.HasAnimatedProperties && meter.Value==30);
            host.Show();settings.Motion=MotionMode.Off;ThemeService.Apply(settings);
            MeterMotion.SetTarget(meter,65);
            check("Off motion applies metrics without animation",!meter.HasAnimatedProperties && meter.Value==65 && !MotionPolicy.Enabled);
            check("Off motion disables hover and material",!(bool)Application.Current.Resources["HoverMotionEnabled"] && !MetallicSurface.MotionEnabled);
            check("Motion profile roundtrip",LayoutProfile.Parse(LayoutProfile.Capture(settings).ToJson()).ApplyTo(new()).Motion==MotionMode.Off);
            settings.Motion=(MotionMode)99;settings.Validate();
            check("Unknown motion mode falls back safely",settings.Motion==MotionMode.Smooth);
            ThemeService.Apply(settings);
            check("Smooth motion retains shared cadence",MotionPolicy.Duration(200).TotalMilliseconds==200);
        }
        finally {host.Close();ThemeService.Apply(new());}
    }
}
