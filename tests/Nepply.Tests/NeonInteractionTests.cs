using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class NeonInteractionTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump, string output, Action<FrameworkElement, int, int, string> render)
    {
        var settings = new AppSettings { Accent = "Neon Cyan", GlowStrength = .3 };
        ThemeService.ApplyAccessibility(settings, false, true);
        var icons = new IconPanel();
        var list = (ListBox)icons.FindName("IconList");
        var item = new ListBoxItem { Style = list.ItemContainerStyle, Content = new TextBlock { Text = "Nepply", Margin = new Thickness(4, 20, 4, 20) }, DataContext = new DesktopIcon { Name = "Nepply", Path = "fixture" } };
        var stage = new Border { Padding = new Thickness(18), Background = (Brush)Application.Current.Resources["CardBrush"], Child = item };
        var host = new Window { Content = stage, Width = 150, Height = 150, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
        try
        {
            host.Show(); pump(40); item.ApplyTemplate();
            var glow = (Border)item.Template.FindName("ItemGlow", item);
            var chrome = (Border)item.Template.FindName("ItemChrome", item);
            check("Icon neon shares chrome silhouette", glow.Margin == new Thickness(0) && glow.CornerRadius == chrome.CornerRadius);
            check("Icon chrome never draws a second hover outline", item.Template.Triggers.OfType<Trigger>().SelectMany(t => t.Setters.OfType<Setter>()).All(s => s.TargetName != "ItemChrome" || s.Property != Border.BorderBrushProperty));
            check("Icon neon effect is isolated from content", glow.Effect != null && chrome.Effect == null);
            glow.Visibility = Visibility.Visible; chrome.Background = (Brush)Application.Current.Resources["AccentWashBrush"];
            render(stage, 150, 150, Path.Combine(output, "icon-single-neon.png"));
            string SurfaceSignature()
            {
                var surface = new Border();
                MetallicSurface.SetAccent(surface, (Brush)Application.Current.Resources["AccentSourceBrush"]);
                var effect = (System.Windows.Media.Effects.DropShadowEffect?)surface.Effect;
                return $"{surface.BorderThickness}|{surface.BorderBrush}|{effect?.BlurRadius}|{effect?.Opacity}|" +
                    string.Join(";", ((GradientBrush)surface.Background).GradientStops.Select(s => $"{s.Offset}:{s.Color}"));
            }
            settings.MetallicEnabled = false; ThemeService.ApplyAccessibility(settings, false, true);
            string neonPreset = SurfaceSignature();
            settings.Accent = "Custom"; settings.CustomAccent = "#39E7F5"; ThemeService.ApplyAccessibility(settings, false, true);
            check("Custom color and Neon preset use identical neon rendering", SurfaceSignature() == neonPreset);
            settings.Accent = "Gold"; ThemeService.ApplyAccessibility(settings, false, true);
            var gold = new Border(); MetallicSurface.SetAccent(gold, (Brush)Application.Current.Resources["AccentSourceBrush"]);
            var goldGlow = (System.Windows.Media.Effects.DropShadowEffect)gold.Effect;
            check("Gold uses full neon style", gold.BorderThickness == new Thickness(1) && goldGlow.BlurRadius == 18 && Math.Abs(goldGlow.Opacity - .48) < .001);
            settings.NeonEnabled = false; ThemeService.ApplyAccessibility(settings, false, true); pump(20);
            check("Neon off removes icon glow on existing visual", glow.Effect == null);
            settings.Accent = "Neon Cyan"; ThemeService.ApplyAccessibility(settings, false, true);
            string disabledPreset = SurfaceSignature();
            settings.Accent = "Custom"; settings.CustomAccent = "#39E7F5"; ThemeService.ApplyAccessibility(settings, false, true);
            check("Neon off never retains special preset material", SurfaceSignature() == disabledPreset);
            var colors = ((GradientBrush)Application.Current.Resources["IslandBorderBrush"]).GradientStops.Select(s => s.Color).ToArray();
            settings.GlowStrength = .5; ThemeService.ApplyAccessibility(settings, false, true);
            check("Disabled neon ignores intensity for panel rim", colors.SequenceEqual(((GradientBrush)Application.Current.Resources["IslandBorderBrush"]).GradientStops.Select(s => s.Color)));
            using (var pointer = new MaterialPointerLight(stage))
            {
                var e = new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.PreviewMouseMoveEvent, Handled = true };
                var position = e.GetPosition(stage);
                stage.RaiseEvent(e);
                pointer.MoveTo(new Point(position.X, position.Y - 20));
                check("Handled mouse event establishes direction baseline", Math.Abs(pointer.Transform.X + 7) < .01 && pointer.Transform.X == pointer.Transform.Y);
                pointer.MoveTo(null);
                check("Mouse leave preserves phase without clocks", Math.Abs(pointer.Transform.X + 7) < .01 && !pointer.Transform.HasAnimatedProperties);
                pointer.MoveTo(new Point(50, 50));
                pointer.MoveTo(new Point(50, 70));
                check("Downward motion reverses diagonal direction", Math.Abs(pointer.Transform.X) < .01 && pointer.Transform.X == pointer.Transform.Y);
                pointer.MoveTo(new Point(150, 70));
                check("Horizontal motion does not jitter light", Math.Abs(pointer.Transform.X) < .01);
                pointer.MoveTo(new Point(150, 70.2));
                check("Subpixel hand jitter ignored", Math.Abs(pointer.Transform.X) < .01);
                for (int i = 0; i < 10000; i++) pointer.MoveTo(new Point(150, 80 + i * 10));
                check("Long pointer interaction remains bounded with no clocks", Math.Abs(pointer.Transform.X) < 240 && !pointer.Transform.HasAnimatedProperties);

            }
            var dialog = new SettingsWindow(settings);
            try
            {
                check("Glow slider disabled while neon is off", !((Slider)dialog.FindName("GlowSlider")).IsEnabled);
                ((CheckBox)dialog.FindName("NeonEnabled")).IsChecked = true;
                check("Glow slider enabled when neon is on", ((Slider)dialog.FindName("GlowSlider")).IsEnabled);
            }
            finally { dialog.Close(); }
        }
        finally { host.Close(); icons.Cleanup(); ThemeService.Apply(new()); }
    }
}
