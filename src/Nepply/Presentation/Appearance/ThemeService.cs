using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Nepply.Models;
using Nepply.Modules.Appearance;
namespace Nepply.Services;

public static class ThemeService
{
    public static readonly IReadOnlyDictionary<string, string> Palettes = new Dictionary<string, string>
    {
        ["Champagne"] = "#D9BC8B",
        ["Gold"] = "#D4AF37",
        ["Platinum"] = "#BCCAD8",
        ["Rose Gold"] = "#E2A99C",
        ["Copper"] = "#DA9A70",
        ["Titanium"] = "#A2ABC8",
        ["Neon Cyan"] = "#39E7F5",
        ["Neon Violet"] = "#B18AFF",
        ["Neon Pink"] = "#FF78C5",
        ["Neon Lime"] = "#B9F56A",
        ["Neon Blue"] = "#6CBAFF",
        ["Custom"] = "#D9BC8B",
        ["Violet"] = "#B8A1EB",
        ["Jade"] = "#8ACAB2",
        ["Blue"] = "#8EBAEE"
    };
    public static void Apply(AppSettings settings)
        => ApplyAccessibility(settings, SystemParameters.HighContrast, SystemParameters.ClientAreaAnimation);
    internal static void ApplyAccessibility(AppSettings settings, bool highContrast, bool clientAnimation)
    {
        Views.MetallicSurface.HighContrast = highContrast;
        MotionPolicy.Configure(settings.Motion);
        Views.MetallicSurface.MaterialEnabled = settings.MetallicEnabled;
        Views.MetallicSurface.NeonEnabled = settings.NeonEnabled;
        LocalizationService.Apply(settings.Language);
        Views.MetallicSurface.NeonPalette = settings.NeonEnabled;
        Views.MetallicSurface.MotionEnabled = settings.DynamicMaterial && settings.Motion != MotionMode.Off && clientAnimation && !highContrast;
        Views.MetallicSurface.GlowStrength = settings.GlowStrength;
        Application.Current.Resources["ScrollRailOpacity"] = highContrast ? 1d : .35d;
        Application.Current.Resources["HoverMotionEnabled"] = settings.Motion != MotionMode.Off && clientAnimation && !highContrast;
        if (highContrast)
        {
            Application.Current.Resources["IconNeonEffect"] = null;
            Application.Current.Resources["IslandBorderBrush"] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "IslandBrush", "SurfaceBrush", "CardBrush", "HoverBrush", "AccentWashBrush", "CardSheenBrush", "OnAccentBrush", "ProgressTrackBrush" })
                Application.Current.Resources[key] = SystemColors.WindowBrush;
            foreach (var key in new[] { "TextBrush", "MutedBrush", "BorderBrush", "AccentBrush", "AccentSourceBrush", "AccentGradientBrush", "ErrorBrush" })
                Application.Current.Resources[key] = SystemColors.WindowTextBrush;
            Application.Current.Resources["ProgressOutlineBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["SelectionBrush"] = SystemColors.HighlightBrush;
            Application.Current.Resources["SelectionTextBrush"] = SystemColors.HighlightTextBrush;
            Application.Current.Resources["DisabledControlBrush"] = SystemColors.WindowBrush;
            Application.Current.Resources["DisabledMarkBrush"] = SystemColors.GrayTextBrush;
            Application.Current.Resources["DisabledTextBrush"] = SystemColors.GrayTextBrush;
            Application.Current.Resources["DisabledBorderBrush"] = SystemColors.GrayTextBrush;
            return;
        }
        Application.Current.Resources["ProgressOutlineBrush"] = Brushes.Transparent;
        bool light = settings.Theme == ThemeOption.Light;
        if (settings.Theme == ThemeOption.System)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        Set("ErrorBrush", light ? "#A52F29" : "#ED8D80");
        Set("SurfaceBrush", light ? "#F5F3EF" : "#141619"); Set("CardBrush", light ? "#FFFFFF" : "#1D2024");
        Set("HoverBrush", light ? "#E9E6E0" : "#292D32"); Set("BorderBrush", light ? "#DDD9D2" : "#34383E");
        Set("ProgressTrackBrush", light ? "#DDD9D2" : "#34383E");
        Set("TextBrush", light ? "#222529" : "#F1F0ED"); Set("MutedBrush", light ? "#696B70" : "#9A9FA7");
        var island = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(light ? "#FAF9F6" : "#202329"),
            (Color)ColorConverter.ConvertFromString(light ? "#F0EEEA" : "#111316"), 90);
        island.Opacity = double.IsFinite(settings.PanelOpacity) ? Math.Clamp(settings.PanelOpacity, .8, 1) : .98;
        island.Freeze(); Application.Current.Resources["IslandBrush"] = island;
        var sourceAccent = (Color)ColorConverter.ConvertFromString(settings.Accent == "Custom" ? settings.CustomAccent : Palettes.GetValueOrDefault(settings.Accent, "#D9BC8B"));
        double iconGlow = settings.NeonEnabled && double.IsFinite(settings.GlowStrength) ? Math.Clamp(settings.GlowStrength, 0, .5) : 0;
        var iconEffect = new DropShadowEffect
        {
            Color = sourceAccent,
            ShadowDepth = 0,
            BlurRadius = 18,
            Opacity = iconGlow * 1.6,
            RenderingBias = RenderingBias.Performance
        };
        iconEffect.Freeze();
        Application.Current.Resources["IconNeonEffect"] = iconGlow > 0 ? iconEffect : null;
        // A lightweight tinted rim keeps the palette visible even with monitor cards disabled.
        var rim = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
        Color Rim(byte alpha) => Color.FromArgb(alpha, sourceAccent.R, sourceAccent.G, sourceAccent.B);
        double glow = iconGlow;
        rim.GradientStops.Add(new(Rim(Views.MetallicSurface.NeonPalette ? (byte)(110 + glow * 180) : (byte)155), 0));
        rim.GradientStops.Add(new(Rim(Views.MetallicSurface.NeonPalette ? (byte)75 : (byte)45), .55));
        rim.GradientStops.Add(new(Rim(Views.MetallicSurface.NeonPalette ? (byte)140 : (byte)110), 1));
        rim.Freeze(); Application.Current.Resources["IslandBorderBrush"] = rim;
        var surfaceColors = (light
            ? new[] { "#FFFFFF", "#F5F3EF", "#FAF9F6", "#F0EEEA", "#E9E6E0" }
            : new[] { "#141619", "#1D2024", "#202329", "#111316", "#292D32" })
            .Select(hex => (Color)ColorConverter.ConvertFromString(hex)).ToList();
        // Functional text must remain legible over the restored material's brightest band.
        var materialBase = (Application.Current.Resources["CardBrush"] as SolidColorBrush)!.Color;
        double tint = Views.MetallicSurface.NeonPalette ? .20 : .32;
        surfaceColors.Add(Color.FromRgb((byte)(materialBase.R * (1 - tint) + sourceAccent.R * tint),
            (byte)(materialBase.G * (1 - tint) + sourceAccent.G * tint), (byte)(materialBase.B * (1 - tint) + sourceAccent.B * tint)));
        var palette = AccentPalette.Create(sourceAccent, surfaceColors);
        var accent = palette.Functional;
        Set("AccentSourceBrush", palette.Source.ToString());
        Set("AccentBrush", accent.ToString());
        var wash = new SolidColorBrush(Color.FromArgb(light ? (byte)24 : (byte)28, accent.R, accent.G, accent.B)); wash.Freeze();
        Application.Current.Resources["AccentWashBrush"] = wash;
        var sheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        var card = (Color)ColorConverter.ConvertFromString(light ? "#FFFFFF" : "#1C1F24");
        double blend = light ? .07 : .13;
        sheen.GradientStops.Add(new GradientStop(Color.FromRgb((byte)(card.R * (1 - blend) + accent.R * blend), (byte)(card.G * (1 - blend) + accent.G * blend), (byte)(card.B * (1 - blend) + accent.B * blend)), 0));
        sheen.GradientStops.Add(new GradientStop(card, .85));
        sheen.Freeze(); Application.Current.Resources["CardSheenBrush"] = sheen;
        var metallic = new LinearGradientBrush(accent, Color.FromRgb((byte)Math.Min(255, accent.R + 34), (byte)Math.Min(255, accent.G + 34), (byte)Math.Min(255, accent.B + 34)), 20);
        metallic.Freeze(); Application.Current.Resources["AccentGradientBrush"] = metallic;
        Set("OnAccentBrush", palette.OnFunctional.ToString());
        // State pairs are independent from accent-on-surface metrics and graphs.
        Application.Current.Resources["SelectionBrush"] = wash;
        Application.Current.Resources["SelectionTextBrush"] = Application.Current.Resources["AccentBrush"];
        Application.Current.Resources["DisabledControlBrush"] = Application.Current.Resources["BorderBrush"];
        Application.Current.Resources["DisabledMarkBrush"] = Application.Current.Resources["TextBrush"];
        Application.Current.Resources["DisabledTextBrush"] = Application.Current.Resources["MutedBrush"];
        Application.Current.Resources["DisabledBorderBrush"] = Application.Current.Resources["BorderBrush"];
    }
    internal static double Contrast(Color a, Color b) => AccentPalette.Contrast(a, b);
    internal static Color TextOnAccent(Color accent) => AccentPalette.TextOn(accent);
    private static void Set(string key, string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); brush.Freeze();
        Application.Current.Resources[key] = brush;
    }
}
