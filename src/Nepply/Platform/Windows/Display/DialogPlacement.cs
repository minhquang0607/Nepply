using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Nepply.Helpers;

internal static class DialogPlacement
{
    internal static Size FitSize(Size requested, Size workArea)
    {
        if (!double.IsFinite(workArea.Width) || !double.IsFinite(workArea.Height)
            || workArea.Width <= 0 || workArea.Height <= 0) return requested;
        return new Size(Math.Min(requested.Width, Math.Max(1, workArea.Width - 24)),
            Math.Min(requested.Height, Math.Max(1, workArea.Height - 24)));
    }
    internal static void FitInitial(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var screen = ScreenManager.ForWindow(handle);
        if (screen == null) return;
        var dpi = VisualTreeHelper.GetDpi(window);
        var current = new Size(window.ActualWidth, window.ActualHeight);
        var fitted = FitSize(current, new Size(screen.Width / dpi.DpiScaleX, screen.Height / dpi.DpiScaleY));
        if (fitted == current) return;
        window.MinWidth = Math.Min(window.MinWidth, fitted.Width);
        window.MinHeight = Math.Min(window.MinHeight, fitted.Height);
        window.Width = fitted.Width; window.Height = fitted.Height;
        int width = (int)Math.Round(fitted.Width * dpi.DpiScaleX);
        int height = (int)Math.Round(fitted.Height * dpi.DpiScaleY);
        ScreenManager.SetWindowPos(handle, IntPtr.Zero, screen.Left + (screen.Width - width) / 2,
            screen.Top + (screen.Height - height) / 2, width, height, 0x0014);
    }
}
