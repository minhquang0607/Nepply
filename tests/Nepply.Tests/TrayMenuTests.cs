using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nepply;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class TrayMenuTests
{
    internal static int Run(string output)
    {
        var app=new App(testHost:true); app.InitializeComponent();
        var window=new MainWindow(preview:true); window.ApplySettings();
        int logs=0,exit=0;
        var menu=new TrayMenu(window,()=>logs++,()=>exit++);
        var results=new List<string>(); int failures=0;
        void Check(string label,bool valid) { results.Add((valid ? "PASS " : "FAIL ")+label); if(!valid) failures++; }
        var pin=(MenuItem)menu.FindName("PinItem");
        Check("Tray starts with actual unpinned state",!pin.IsChecked);
        window.TogglePin(); menu.RefreshStatus();
        Check("Tray synchronizes pin changes made on panel",pin.IsChecked);
        pin.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check("Tray pin command updates shared panel state",!window.IsPinned);
        foreach(var edge in Enum.GetValues<EdgePosition>())
        {
            window.Settings.EdgePosition=edge; menu.RefreshStatus();
            Check("Tray reflects current dock edge: "+edge,((TextBlock)menu.FindName("StatusLabel")).Text.StartsWith(LocalizationService.Get(edge.ToString())+" · "));
        }
        Check("Tray shows configured keyboard shortcut",((MenuItem)menu.FindName("OpenItem")).InputGestureText==MainWindow.HotkeyLabel(window.Settings));
        Check("Tray shows assembly version",!string.IsNullOrWhiteSpace(((TextBlock)menu.FindName("VersionLabel")).Text));
        var commands=menu.Items.OfType<MenuItem>().ToArray();
        commands.Single(i=>System.Windows.Automation.AutomationProperties.GetName(i)==LocalizationService.Get("Logs")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        commands.Single(i=>System.Windows.Automation.AutomationProperties.GetName(i)==LocalizationService.Get("Exit")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check("Tray routes log and guarded exit callbacks once",logs==1 && exit==1);
        foreach(var (theme,name) in new[]{(ThemeOption.Dark,"dark"),(ThemeOption.Light,"light")})
        {
            ThemeService.Apply(new AppSettings {Theme=theme});
            menu.RefreshStatus();
            menu.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            menu.Visibility=Visibility.Visible;
            menu.Measure(new Size(302,double.PositiveInfinity));
            menu.Arrange(new Rect(new Point(),menu.DesiredSize)); menu.UpdateLayout();
            var surface=(Border)menu.Template.FindName("MenuSurface",menu);
            Check("Tray "+name+" follows active material theme",surface.Background is LinearGradientBrush {GradientStops.Count:17} gradient
                && gradient.GradientStops.All(s=>s.Color.A==255)
                && (theme==ThemeOption.Light ? gradient.GradientStops[0].Color.R>200 : gradient.GradientStops[0].Color.R<80));
            Check("Tray "+name+" hidden/unloaded surface does not start material motion",MetallicSurface.GetAnimate(surface)
                && surface.Background is LinearGradientBrush idle && !idle.RelativeTransform.HasAnimatedProperties);
            Check("Tray "+name+" fits compact menu bounds",menu.ActualWidth==302 && menu.ActualHeight>250 && menu.ActualHeight<520);
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render(menu);
            var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(output,"tray-"+name+".png")); png.Save(file);
        }
        window.Cleanup(); window.Close();
        results.Add($"RESULT: {results.Count-failures} passed; {failures} failed. Offline WPF rendering and callbacks; no native Shell command invoked.");
        File.WriteAllLines(Path.Combine(output,"verification.txt"),results);
        foreach(var line in results) Console.WriteLine(line);
        return failures==0 ? 0 : 1;
    }
}
