using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Nepply.Models;
using Nepply.Modules.Appearance;
using Nepply.Services;
using Nepply.Views;

internal static class StateAppearanceTests
{
    internal static void Run(Action<string,bool> check, Action<int> pump, string output,
        Action<FrameworkElement,int,int,string> render)
    {
        foreach (var mode in new[] { "Dark", "Light", "Contrast" })
        {
            bool contrast = mode == "Contrast";
            var settings = new AppSettings { Theme = mode == "Light" ? ThemeOption.Light : ThemeOption.Dark };
            ThemeService.ApplyAccessibility(settings, contrast, false);
            var content = new StackPanel { Margin = new Thickness(20), Background = (Brush)Application.Current.FindResource("SurfaceBrush") };
            var samples = new List<(CheckBox Control, bool Enabled, bool? State)>();
            foreach (bool enabled in new[] { true, false })
            foreach (bool? state in new bool?[] { false, true, null })
            {
                var box = new CheckBox { Content = $"{(enabled ? "Enabled" : "Disabled")} / {state?.ToString() ?? "Mixed"}",
                    IsThreeState = true, IsEnabled = enabled, IsChecked = state };
                content.Children.Add(box); samples.Add((box,enabled,state));
            }
            var navigation = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                ItemContainerStyle = (Style)Application.Current.FindResource("SettingsNavigationItem"),
                ItemsSource = new[] { "Đang chọn", "Chưa chọn" }, SelectedIndex = 0 };
            content.Children.Add(navigation);
            var metric = new TextBlock { Text = "100% 100°C", Style = (Style)Application.Current.FindResource("MetricValue") };
            content.Children.Add(metric);
            var ramText = new TextBlock { Text = "11,13 / 13,90 GiB", Style = (Style)Application.Current.FindResource("NumericText") };
            content.Children.Add(ramText);
            var host = new Window { Content = content, Width = 400, Height = 520, Left = -10000, Top = -10000,
                ShowActivated = false, ShowInTaskbar = false };
            try
            {
                host.Show(); pump(30); host.UpdateLayout();
                foreach (var sample in samples)
                {
                    var box = (Border)sample.Control.Template.FindName("Box",sample.Control);
                    var mark = (TextBlock)sample.Control.Template.FindName("Mark",sample.Control);
                    string name = $"States {mode}/{sample.Enabled}/{sample.State?.ToString() ?? "Mixed"}";
                    check(name + " preserves checked/mixed/unchecked glyph", sample.State == false
                        ? mark.Visibility == Visibility.Collapsed
                        : mark.Visibility == Visibility.Visible && mark.Text == (sample.State == null ? "−" : "✓"));
                    if (sample.State != false)
                    {
                        double ratio = ThemeService.Contrast(((SolidColorBrush)mark.Foreground).Color,((SolidColorBrush)box.Background).Color);
                        // Disabled colors are supplied by the user's system theme, not forced to a branded threshold.
                        check(name + " mark contrasts with its actual fill", ratio >= (sample.Enabled ? 4.5 : 1.01));
                    }
                }
                var selected = (ListBoxItem)navigation.ItemContainerGenerator.ContainerFromIndex(0);
                var chrome = (Border)selected.Template.FindName("TabChrome",selected);
                var indicator = (Border)selected.Template.FindName("SelectionIndicator",selected);
                check($"States {mode} selection retains independent marker", indicator.Visibility == Visibility.Visible
                    && Equals(chrome.Background,Application.Current.FindResource("SelectionBrush")));
                check($"States {mode} navigation text uses selection foreground", Descendants(selected).OfType<TextBlock>()
                    .Any(t => t.Text == "Đang chọn" && Equals(t.Foreground,Application.Current.FindResource("SelectionTextBrush"))));
                if (contrast)
                    check("States contrast selection uses system highlight pair", ReferenceEquals(chrome.Background,SystemColors.HighlightBrush)
                        && ReferenceEquals(selected.Foreground,SystemColors.HighlightTextBrush));
                check($"States {mode} selection keeps native automation", new ListBoxAutomationPeer(navigation).GetPattern(PatternInterface.Selection) != null);
                navigation.SelectedIndex = 1; pump(10);
                check($"States {mode} old selection clears marker", indicator.Visibility == Visibility.Collapsed);
                check($"States {mode} metric uses lining tabular figures", Typography.GetNumeralAlignment(metric) == FontNumeralAlignment.Tabular
                    && Typography.GetNumeralStyle(metric) == FontNumeralStyle.Lining && metric.FontSize == 25);
                PresentationDensity.SetCompact(metric,true);
                check($"States {mode} compact typography preserves policy", metric.FontSize == 20 && Typography.GetNumeralAlignment(metric) == FontNumeralAlignment.Tabular);
                check($"States {mode} numeric text retains readable theme foreground", Equals(ramText.Foreground,Application.Current.FindResource("TextBrush"))
                    && ThemeService.Contrast(((SolidColorBrush)ramText.Foreground).Color,((SolidColorBrush)content.Background).Color) >= 4.5);
                render(content,400,500,Path.Combine(output,$"states-{mode}.png"));
                ThemeService.ApplyAccessibility(settings,!contrast,false); pump(10);
                var nowSelected = (ListBoxItem)navigation.ItemContainerGenerator.ContainerFromIndex(1);
                var nowChrome = (Border)nowSelected.Template.FindName("TabChrome",nowSelected);
                check($"States {mode} existing selection updates theme pair", Equals(nowChrome.Background,Application.Current.FindResource("SelectionBrush"))
                    && Equals(nowSelected.Foreground,Application.Current.FindResource("SelectionTextBrush")));
            }
            finally { host.Close(); }

            ThemeService.ApplyAccessibility(settings,contrast,false);
            var dialog = new SettingsWindow(settings);
            try
            {
                var root = (FrameworkElement)dialog.Content;
                render(root,820,740,Path.Combine(output,$"settings-states-{mode}.png"));
                var query = (TextBox)dialog.FindName("SearchSettings");
                var trigger = (Button)dialog.FindName("SettingsSearchTrigger");
                query.Text = "màu";
                root.Measure(new Size(820,740)); root.Arrange(new Rect(0,0,820,740)); root.UpdateLayout(); pump(20);
                check($"States {mode} settings search announces open", new ButtonAutomationPeer(trigger).GetItemStatus() == "Tìm kiếm đang mở");
                var results = (ListBox)dialog.FindName("SearchResults");
                results.SelectedIndex = 0; root.UpdateLayout();
                var row = (ListBoxItem)results.ItemContainerGenerator.ContainerFromIndex(0);
                var rowChrome = (Border)row.Template.FindName("ResultChrome",row);
                check($"States {mode} search selection retains state pair", Equals(rowChrome.Background,Application.Current.FindResource("SelectionBrush"))
                    && Descendants(row).OfType<TextBlock>().Any(t => Equals(t.Foreground,Application.Current.FindResource("SelectionTextBrush"))));
                var action = Descendants(row).OfType<Button>().Single();
                check($"States {mode} inner action cannot cover selection", action.Template.Triggers.Count == 0);
                render(root,820,740,Path.Combine(output,$"settings-search-states-{mode}.png"));
                typeof(SettingsWindow).GetMethod("ExpandSettingsSearch",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dialog,[false]);
                check($"States {mode} search clears open announcement", AutomationProperties.GetItemStatus(trigger) == "");
            }
            finally { dialog.Close(); }
        }
        ThemeService.ApplyAccessibility(new AppSettings(),false,true);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
}
