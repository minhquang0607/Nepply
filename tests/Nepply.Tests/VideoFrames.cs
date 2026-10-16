using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class VideoFrames
{
    public static void Capture(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        var player = new MediaPlayer { IsMuted = true, ScrubbingEnabled = true };
        bool ready = false; string? error = null;
        player.MediaOpened += (_, _) => ready = true;
        player.MediaFailed += (_, e) => error = e.ErrorException.Message;
        player.Open(new Uri(Path.GetFullPath(source)));
        for (int i = 0; i < 100 && !ready && error == null; i++) Pump(100);
        if (!ready) { player.Close(); throw new InvalidOperationException(error ?? "Video decoder did not open"); }
        var duration = player.NaturalDuration.TimeSpan.TotalSeconds;
        Console.WriteLine($"Video: {duration:F2}s, {player.NaturalVideoWidth}x{player.NaturalVideoHeight}");
        player.Play(); Pump(200); player.Pause();
        for (int i = 0; i < 20; i++)
        {
            double second = duration * i / 20;
            player.Position = TimeSpan.FromSeconds(second); Pump(150);
            int width = Math.Min(1280, player.NaturalVideoWidth);
            int height = (int)Math.Round(width * (double)player.NaturalVideoHeight / player.NaturalVideoWidth);
            var visual = new DrawingVisual(); using (var drawing = visual.RenderOpen()) drawing.DrawVideo(player, new Rect(0,0,width,height));
            var bitmap = new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(destination,$"frame-{i:00}-{second:F2}s.png")); encoder.Save(stream);
        }
        player.Close();
    }
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
}
