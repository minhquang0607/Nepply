using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nepply;
using Nepply.Helpers;
using Nepply.Models;
using Nepply.Views;

internal static class DpiLayoutTests
{
    internal static int Run(string output)
    {
        var app = new App(testHost: true); app.InitializeComponent();
        var results = new List<string>(); int failures = 0;
        foreach (double scale in new[] { 1, 1.25, 1.5, 1.75, 2, 2.5 })
            foreach (var pixels in new[] { new Size(1920, 1040), new Size(1366, 728) })
            {
                var window = new SettingsWindow(new AppSettings());
                var root = (FrameworkElement)window.Content;
                var size = DialogPlacement.FitSize(new Size(window.Width, window.Height), new Size(pixels.Width / scale, pixels.Height / scale));
                window.ApplyCompactLayout(size.Height);
                root.Width = size.Width; root.Height = size.Height;
                root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
                var buttons = Descendants(root).OfType<Button>().Where(b => b.Name is "ApplyButton" or "OkButton" || Equals(b.Content, Nepply.Services.LocalizationService.Get("Cancel"))).ToArray();
                bool visible = buttons.Length == 3 && buttons.All(b =>
                {
                    var rect = b.TransformToAncestor(root).TransformBounds(new Rect(b.RenderSize));
                    return rect.Width > 0 && rect.Height > 0 && rect.Left >= 0 && rect.Top >= 0 && rect.Right <= size.Width + .5 && rect.Bottom <= size.Height + .5;
                });
                var scroll = (ScrollViewer)window.FindName("SettingsScroll");
                visible &= scroll.ActualHeight >= 80;
                string label = $"{pixels.Width}x{pixels.Height}-{scale * 100:0}";
                results.Add($"{(visible ? "PASS" : "FAIL")} Settings save/cancel fit {label}%"); if (!visible) failures++;
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(root); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(Path.Combine(output, $"settings-{label}.png"))) png.Save(file);
                window.Close();
            }
        results.Add($"RESULT: {results.Count - failures} passed; {failures} failed. DPI raster/layout fixtures only; no Windows scale change, WM_DPICHANGED or multi-monitor certification.");
        File.WriteAllLines(Path.Combine(output, "verification.txt"), results);
        foreach (var line in results) Console.WriteLine(line);
        app.Shutdown(); return failures == 0 ? 0 : 1;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
