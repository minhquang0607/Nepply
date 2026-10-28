using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using Nepply.Models;
using Nepply.Services;

internal static class InputAppearanceTests
{
    internal static void Run(Action<string, bool> check, Action<int> pump, string output,
        Action<FrameworkElement, int, int, string> render)
    {
        foreach (var theme in new[] { ThemeOption.Dark, ThemeOption.Light })
        {
            ThemeService.ApplyAccessibility(new AppSettings { Theme = theme }, false, true);
            var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
            var choice = new ComboBox { ItemsSource = new[] { "Một", "Hai" }, SelectedIndex = 0 };
            var toggle = new CheckBox { Content = "Tùy chọn", IsThreeState = true, IsChecked = true };
            var stack = new StackPanel(); stack.Children.Add(slider); stack.Children.Add(choice); stack.Children.Add(toggle);
            var host = new Window { Content = stack, Width = 300, Height = 220, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                host.Show(); pump(30); host.UpdateLayout();
                var track = (Track)slider.Template.FindName("PART_Track", slider);
                check($"Inputs {theme} slider has no rectangular drag border", ((Border)slider.Template.FindName("SliderFocus", slider)).BorderThickness == new Thickness(0));
                check($"Inputs {theme} slider has generous thumb target", track.Thumb.ActualWidth == 28 && track.Thumb.ActualHeight == 28);
                check($"Inputs {theme} slider repeat targets share height", track.DecreaseRepeatButton.ActualHeight == 28 && track.IncreaseRepeatButton.ActualHeight == 28);
                double before = slider.Value;
                Slider.IncreaseSmall.Execute(null, slider);
                check($"Inputs {theme} slider retains native command", slider.Value > before);
                check($"Inputs {theme} slider retains RangeValue automation", new SliderAutomationPeer(slider).GetPattern(PatternInterface.RangeValue) != null);
                foreach (var flow in new[] { FlowDirection.LeftToRight, FlowDirection.RightToLeft })
                    foreach (bool reverse in new[] { false, true })
                    {
                        slider.FlowDirection = flow; slider.IsDirectionReversed = reverse;
                        foreach (double value in new[] { 0d, .1d, 10d, 50d, 90d, 100d })
                        {
                            slider.Value = value; host.UpdateLayout();
                            var rail = (Nepply.Views.SliderRail)slider.Template.FindName("Rail", slider);
                            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(rail.ActualWidth), 28, 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(rail);
                            var accent = ((SolidColorBrush)slider.Foreground).Color;
                            var background = ((SolidColorBrush)rail.Background).Color;
                            // Inspect the continuous rail alone so the thumb/hover halo cannot conceal defects.
                            bool correct = true;
                            double fraction = value / 100, end = 14 + (rail.ActualWidth - 28) * fraction;
                            for (int x = 2; x < bitmap.PixelWidth - 2; x++)
                            {
                                double logical = reverse ? rail.ActualWidth - x - .5 : x + .5;
                                if (Math.Abs(logical - 14) < 2 || Math.Abs(logical - end) < 2 || Math.Abs(logical - (rail.ActualWidth - 14)) < 2) continue;
                                byte[] pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(x, 14, 1, 1), pixel, 4, 0);
                                if (logical < 12 || logical > rail.ActualWidth - 12) { correct &= pixel[3] == 0; continue; }
                                var expected = value > 0 && logical < end ? accent : background;
                                correct &= pixel[3] == 255 && Math.Abs(pixel[0] - expected.B) <= 4 && Math.Abs(pixel[1] - expected.G) <= 4 && Math.Abs(pixel[2] - expected.R) <= 4;
                            }
                            check($"Inputs {theme}/{flow}/{reverse}/{value} rail has no end stub or gap", correct);
                        }
                        slider.Value = 50; host.UpdateLayout();
                        render(stack, 300, 160, Path.Combine(output, $"input-rail-{theme}-{flow}-{reverse}.png"));
                    }
                slider.FlowDirection = FlowDirection.LeftToRight; slider.IsDirectionReversed = false;
                var drop = (ToggleButton)choice.Template.FindName("DropToggle", choice);
                var chevron = (System.Windows.Shapes.Path)drop.Template.FindName("DropChevron", drop);
                check($"Inputs {theme} choice uses a readable vector chevron", chevron.Width == 8 && chevron.Height == 4
                    && chevron.StrokeThickness == 1.5 && Equals(chevron.Stroke, choice.Foreground));
                choice.IsDropDownOpen = true; pump(20);
                check($"Inputs {theme} open choice exposes accent state", Equals(drop.BorderBrush, Application.Current.FindResource("AccentBrush")));
                check($"Inputs {theme} open chevron points up", chevron.RenderTransform is RotateTransform { Angle: 180 });
                choice.IsDropDownOpen = false;
                check($"Inputs {theme} closed chevron resets its direction", chevron.RenderTransform is not RotateTransform { Angle: 180 });
                choice.IsEnabled = false; pump(10);
                check($"Inputs {theme} disabled choice has distinct surface", Equals(drop.Background, Application.Current.FindResource("SurfaceBrush")));
                var mark = (TextBlock)toggle.Template.FindName("Mark", toggle);
                toggle.IsChecked = null;
                check($"Inputs {theme} mixed checkbox has distinct mark", mark.Text == "−" && mark.Visibility == Visibility.Visible);
                toggle.IsChecked = false;
                check($"Inputs {theme} unchecked checkbox clears mark", mark.Visibility == Visibility.Collapsed);
                toggle.IsChecked = true; toggle.IsEnabled = false; pump(10);
                check($"Inputs {theme} disabled check mark follows theme", Equals(mark.Foreground, Application.Current.FindResource("DisabledMarkBrush")));
                slider.IsEnabled = false;
                check($"Inputs {theme} disabled slider uses muted brush", Equals(slider.Foreground, Application.Current.FindResource("MutedBrush")));
                ThemeService.ApplyAccessibility(new AppSettings(), true, true); pump(10);
                var box = (Border)toggle.Template.FindName("Box", toggle);
                check($"Inputs {theme} existing checkbox updates high contrast", Equals(mark.Foreground, SystemColors.GrayTextBrush)
                    && Equals(box.Background, SystemColors.WindowBrush));
            }
            finally { host.Close(); }
        }
        ThemeService.ApplyAccessibility(new AppSettings(), false, true);
    }
}
