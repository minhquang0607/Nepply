using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nepply.Models;

namespace Nepply.Services;

/// <summary>Reuse unchanged items without mutating objects already bound to the UI.</summary>
internal static class DesktopSnapshot
{
    internal static DesktopIcon[] Reconcile(IEnumerable<DesktopIcon> previous, IEnumerable<DesktopIcon> incoming)
    {
        var old = new Dictionary<string, DesktopIcon>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in previous) old.TryAdd(item.Path, item);
        return incoming.Select(item => old.TryGetValue(item.Path, out var before) && Same(before, item)
            ? before : item).ToArray();
    }

    private static bool Same(DesktopIcon a, DesktopIcon b) =>
        a.Path == b.Path && a.Name == b.Name && a.IsSystemIcon == b.IsSystemIcon
        && a.SourcePaths.SequenceEqual(b.SourcePaths, StringComparer.Ordinal)
        && SameImage(a.Icon, b.Icon);

    private static bool SameImage(ImageSource? a, ImageSource? b)
    {
        if (ReferenceEquals(a, b)) return true;
        // Only compare small immutable thumbnails; unknown/larger images refresh normally.
        if (a is not BitmapSource x || b is not BitmapSource y || !x.IsFrozen || !y.IsFrozen
            || x.PixelWidth != y.PixelWidth || x.PixelHeight != y.PixelHeight
            || x.Format != y.Format || x.DpiX != y.DpiX || x.DpiY != y.DpiY) return false;
        if (!(x.Palette?.Colors ?? []).SequenceEqual(y.Palette?.Colors ?? [])) return false;
        long stride = ((long)x.PixelWidth * x.Format.BitsPerPixel + 7) / 8;
        long length = stride * x.PixelHeight;
        if (length <= 0 || length > 256 * 1024) return false;
        var left = System.Buffers.ArrayPool<byte>.Shared.Rent((int)length);
        var right = System.Buffers.ArrayPool<byte>.Shared.Rent((int)length);
        try
        {
            x.CopyPixels(left, (int)stride, 0); y.CopyPixels(right, (int)stride, 0);
            return left.AsSpan(0, (int)length).SequenceEqual(right.AsSpan(0, (int)length));
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(left);
            System.Buffers.ArrayPool<byte>.Shared.Return(right);
        }
    }
}
