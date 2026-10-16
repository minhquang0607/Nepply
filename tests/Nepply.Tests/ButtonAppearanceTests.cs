using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nepply.Models;
using Nepply.Services;

internal static class ButtonAppearanceTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump)
    {
        var style = (Style)Application.Current.FindResource("PrimaryButton");
        foreach (var theme in new[] { ThemeOption.Dark, ThemeOption.Light })
        foreach (var color in new[] { "#000000", "#FFFFFF", "#D9BC8B" })
        {
            ThemeService.ApplyAccessibility(new AppSettings { Theme = theme, Accent = "Custom", CustomAccent = color }, false, true);
            var button = new Button { Style = style, Content = "Lưu thay đổi" };
            button.ApplyTemplate();
            button.Measure(new Size(300, 100));
            button.Arrange(new Rect(button.DesiredSize));
            pump(10); // Resolve the generated text's ancestor binding after template construction.
            var chrome = (Border)button.Template.FindName("Chrome", button);
            check($"Primary button {theme}/{color} renders readable colors",
                chrome.Background is SolidColorBrush background && button.Foreground is SolidColorBrush foreground
                && ThemeService.Contrast(background.Color, foreground.Color) >= 4.5);
            var text = FindText(button);
            check($"Primary button {theme}/{color} actual glyph uses on-accent color",
                text != null && Equals(text.Foreground, button.Foreground));
            check($"Primary button {theme}/{color} reserves stable focus geometry",
                chrome.BorderThickness == new Thickness(1) && button.FocusVisualStyle != null
                && button.Template.FindName("FocusRing", button) == null);
            var focusVisual = new Control { Style = button.FocusVisualStyle };
            focusVisual.ApplyTemplate();
            var outline = (Border)focusVisual.Template.FindName("KeyboardOutline", focusVisual);
            check($"Primary button {theme}/{color} keyboard outline stays outside the fill",
                outline.Margin == new Thickness(-3) && outline.CornerRadius == new CornerRadius(18)
                && outline.Background == Brushes.Transparent && !outline.IsHitTestVisible);
            button.IsEnabled = false;
            check($"Primary button {theme}/{color} uses disabled surface",
                ReferenceEquals(chrome.Background, Application.Current.FindResource("CardBrush"))
                && ReferenceEquals(button.Foreground, Application.Current.FindResource("MutedBrush")));
        }
        // Structural checks complement rendering; they do not simulate real mouse/focus input.
        var sample = new Button { Style = style }; sample.ApplyTemplate();
        var active = sample.Template.Triggers.OfType<Trigger>()
            .Where(t => t.Property == UIElement.IsMouseOverProperty || t.Property == System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty
                || t.Property == UIElement.IsKeyboardFocusedProperty).ToArray();
        check("Primary template separates pointer feedback from keyboard focus visual", active.Length == 2 && sample.FocusVisualStyle != null);
        check("Primary active states preserve colors opacity and layout", active.All(t => t.Setters.OfType<Setter>().All(s =>
            s.TargetName == "Chrome" && s.Property == Border.BorderBrushProperty)));
        var host = new Window { Content = sample, Width = 220, Height = 100, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            host.Show(); pump(30);
            ThemeService.ApplyAccessibility(new AppSettings(), true, true); pump(30);
            check("Primary button updates existing instance for high contrast",
                ReferenceEquals(sample.Background, SystemColors.WindowTextBrush)
                && ReferenceEquals(sample.Foreground, SystemColors.WindowBrush));
        }
        finally { host.Close(); ThemeService.ApplyAccessibility(new AppSettings(), false, true); }
    }
    private static TextBlock? FindText(DependencyObject node)
    {
        if (node is TextBlock text) return text;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindText(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
        return null;
    }
}
