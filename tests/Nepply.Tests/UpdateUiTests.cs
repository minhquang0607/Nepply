using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using Nepply.Modules.Updates;

internal static class UpdateUiTests
{
    internal static void Run(string output, Action<string, bool> check, Action<int> pump)
    {
        var fixture = JsonSerializer.SerializeToUtf8Bytes(new[] { new {
            tag_name = "v99.0.0", draft = false, prerelease = false,
            body = "Nepply 99.0.0 — dữ liệu kiểm thử\n\n• Cải thiện giao diện Settings.\n• Tăng độ ổn định khi cập nhật.\n\n<script>Không thực thi HTML</script>",
            assets = new[] { new { name = "Nepply-99.0.0-Setup.exe", size = 100,
                digest = "sha256:" + new string('a', 64),
                browser_download_url = "https://github.com/minhquang0607/Nepply/releases/download/v99.0.0/Nepply-99.0.0-Setup.exe" } }
        } });
        var window = new UpdateWindow(new UpdateService(new Handler(fixture)));
        try
        {
            window.Show(); pump(300);
            check("Updater UI displays release version", window.StatusText.Contains("v99.0.0"));
            check("Updater UI renders notes literally", window.ReleaseNotes.Contains("<script>"));
            check("Updater UI does not enable unconfigured installation", !window.CanInstall);
            check("Updater has branded window icon", window.Icon != null);
            Capture(window, output, "update-preview.png");
            window.Width = 440; window.Height = 420; pump(100);
            var install = (Button)window.FindName("_install");
            var position = install.TransformToAncestor(window).Transform(new Point(0, 0));
            check("Updater actions stay inside compact window", position.Y >= 0 && position.Y + install.ActualHeight <= window.ActualHeight && position.X + install.ActualWidth <= window.ActualWidth);
            Capture(window, output, "update-compact.png");
            ((FrameworkElement)window.FindName("ProgressSection")).Visibility = Visibility.Visible;
            window.PresentDownloadProgress(43); pump(50);
            var progress = (ProgressBar)window.FindName("_progress");
            check("Updater displays determinate transfer progress", progress.Value == 43 && !progress.IsIndeterminate);
            window.PresentDownloadProgress(100); pump(50);
            check("Updater shows verification rather than stuck 100 percent", progress.IsIndeterminate && ((TextBlock)window.FindName("ProgressPercent")).Text == "");
        }
        finally { window.Close(); }
        var slow = new Handler([], delay: true);
        var cancelled = new UpdateWindow(new UpdateService(slow));
        cancelled.Show(); pump(50); cancelled.Close();
        check("Updater UI close keeps service alive while a request is pending", !slow.Disposed);
        pump(100);
        check("Updater UI close cancels pending request", slow.Cancelled);
        check("Updater UI releases service only after the request unwinds", slow.Disposed && !slow.DisposedDuringSend);
        var idle = new Handler("[]"u8.ToArray());
        var finished = new UpdateWindow(new UpdateService(idle));
        finished.Show(); pump(100); finished.Close();
        check("Updater UI releases an idle service on close", idle.Disposed);
        var error = new UpdateWindow(new UpdateService(new Handler([], status: HttpStatusCode.ServiceUnavailable)));
        try { error.Show(); pump(100); check("Updater error offers retry without install", error.CanRetry && !error.CanInstall); Capture(error, output, "update-error.png"); }
        finally { error.Close(); }
        var empty = new UpdateWindow(new UpdateService(new Handler("[]"u8.ToArray())));
        try { empty.Show(); pump(100); check("Updater no-release state hides update action", ((Button)empty.FindName("_install")).Visibility == Visibility.Collapsed); Capture(empty, output, "update-empty.png"); }
        finally { empty.Close(); }
    }
    private static void Capture(Window window, string output, string name)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        using var file = File.Create(Path.Combine(output, name));
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
    private sealed class Handler(byte[] content, bool delay = false, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        internal bool Cancelled, Disposed, DisposedDuringSend;
        private int _sending;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref _sending);
            try
            {
                if (delay)
                {
                    try { await Task.Delay(Timeout.Infinite, token); }
                    catch (OperationCanceledException) { Cancelled = true; throw; }
                }
                return new(status) { Content = new ByteArrayContent(content) };
            }
            finally { Interlocked.Decrement(ref _sending); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { Disposed = true; DisposedDuringSend |= Volatile.Read(ref _sending) > 0; }
            base.Dispose(disposing);
        }
    }
}
