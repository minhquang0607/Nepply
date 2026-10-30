using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class SwatchAppearanceTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump, string output,
        Action<FrameworkElement, int, int, string> render)
    {
        foreach (var theme in new[] { ThemeOption.Dark, ThemeOption.Light })
        {
            ThemeService.ApplyAccessibility(new AppSettings { Theme = theme }, false, true);
            var settings = new SettingsWindow(new AppSettings { Theme = theme, Accent = "Champagne", CustomAccent = "#C1D2E3" });
            try
            {
                var root = (FrameworkElement)settings.Content;
                root.Measure(new Size(820, 740)); root.Arrange(new Rect(0, 0, 820, 740)); root.UpdateLayout(); pump(30);
                var swatches = (ListBox)settings.FindName("PaletteSwatches");
                var accent = (ComboBox)settings.FindName("AccentChoice");
                var custom = (TextBox)settings.FindName("CustomAccent");
                check($"Swatch {theme} mirrors initial accent", swatches.SelectedValue is "Champagne");
                var item = (ListBoxItem)swatches.ItemContainerGenerator.ContainerFromIndex(0);
                var preview = (Ellipse)item.Template.FindName("Preview", item);
                var mark = (Border)item.Template.FindName("SelectionMark", item);
                var palette = (SettingsWindow.Palette)item.DataContext;
                check($"Swatch {theme} retains frozen reusable preview", ReferenceEquals(preview.Fill, palette.Preview) && palette.Preview.IsFrozen && ReferenceEquals(palette.Preview, palette.Preview));
                check($"Swatch {theme} marks selected palette", mark.Visibility == Visibility.Visible);
                var active = item.Template.Triggers.OfType<Trigger>().Where(t => t.Property == UIElement.IsMouseOverProperty
                    || t.Property == UIElement.IsKeyboardFocusWithinProperty || t.Property == ListBoxItem.IsSelectedProperty).ToArray();
                // Structural state checks are not a substitute for physical pointer/focus testing.
                check($"Swatch {theme} feedback preserves preview and layout", active.Length == 3 && active.All(t => t.Setters.OfType<Setter>().All(s =>
                    s.TargetName != "Preview" && s.Property is var property && (property == Border.BorderBrushProperty || property == UIElement.VisibilityProperty))));
                var peer = new ListBoxAutomationPeer(swatches);
                check($"Swatch {theme} exposes native single selection", peer.GetPattern(PatternInterface.Selection) is ISelectionProvider { CanSelectMultiple: false });
                var cyan = swatches.Items.Cast<SettingsWindow.Palette>().Single(p => p.Name == "Neon Cyan");
                var selection = (ISelectionItemProvider)new ListBoxItemAutomationPeer(cyan, peer).GetPattern(PatternInterface.SelectionItem);
                selection.Select(); pump(10);
                check($"Swatch {theme} automation updates draft control", selection.IsSelected && accent.SelectedItem is "Neon Cyan");
                check($"Swatch {theme} deselection clears old mark", mark.Visibility == Visibility.Collapsed && ReferenceEquals(preview.Fill, palette.Preview));
                foreach (var name in new[] { "Custom", "Blue", "Violet", "Jade" })
                {
                    accent.SelectedItem = name; pump(10);
                    check($"Swatch {theme}/{name} keeps absent palette and custom color", accent.SelectedItem is string current && current == name
                        && swatches.SelectedItem == null && custom.Text == "#C1D2E3");
                }
                accent.SelectedItem = "Platinum"; pump(10);
                check($"Swatch {theme} external choice restores selection", swatches.SelectedValue is "Platinum");
                render(root, 820, 740, System.IO.Path.Combine(output, $"swatch-settings-{theme}.png"));
                ThemeService.ApplyAccessibility(new AppSettings(), true, true); pump(10);
                var selectedItem = (ListBoxItem)swatches.ItemContainerGenerator.ContainerFromItem(swatches.SelectedItem);
                var selectedMark = (Border)selectedItem.Template.FindName("SelectionMark", selectedItem);
                check($"Swatch {theme} existing selection updates high contrast", ReferenceEquals(selectedMark.Background, SystemColors.WindowTextBrush));
                check($"Swatch {theme} owns no decorative clock", !preview.HasAnimatedProperties && !mark.HasAnimatedProperties);
            }
            finally { settings.Close(); }
        }
        ThemeService.ApplyAccessibility(new AppSettings(), false, true);
    }
}
