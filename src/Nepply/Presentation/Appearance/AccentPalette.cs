using System.Windows.Media;

namespace Nepply.Modules.Appearance;

/// <summary>Pure color policy. Source is preserved; functional accent is adapted for theme surfaces.</summary>
internal readonly record struct AccentPalette(Color Source, Color Functional, Color OnFunctional)
{
    internal static AccentPalette Create(Color source, IReadOnlyList<Color> surfaces)
    {
        source = Color.FromRgb(source.R, source.G, source.B);
        if (surfaces.Count == 0) throw new ArgumentException("At least one theme surface is required.", nameof(surfaces));
        bool Readable(Color value) => surfaces.All(surface => Contrast(value, surface) >= 4.5);
        if (Readable(source)) return new(source, source, TextOn(source));

        // Scan quantized RGB candidates toward both endpoints. This is bounded,
        // runs on theme changes only, and checks the final rounded color rather
        // than trusting a floating-point interpolation to meet the threshold.
        for (int step = 1; step <= 255; step++)
        {
            Color lighter = Mix(source, Colors.White, step);
            Color darker = Mix(source, Colors.Black, step);
            if (Readable(lighter)) return new(source, lighter, TextOn(lighter));
            if (Readable(darker)) return new(source, darker, TextOn(darker));
        }
        throw new ArgumentException("Theme surfaces cannot share a readable accent.", nameof(surfaces));
    }

    private static Color Mix(Color from, Color to, int step) => Color.FromRgb(
        (byte)((from.R * (255 - step) + to.R * step + 127) / 255),
        (byte)((from.G * (255 - step) + to.G * step + 127) / 255),
        (byte)((from.B * (255 - step) + to.B * step + 127) / 255));

    internal static double Contrast(Color a, Color b)
    {
        static double Channel(byte component) { double c = component / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        static double Luminance(Color c) => .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        double first = Luminance(a), second = Luminance(b);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

    internal static Color TextOn(Color accent)
    {
        var graphite = Color.FromRgb(16, 18, 21);
        if (Contrast(graphite, accent) >= 4.5) return graphite;
        return Contrast(Colors.White, accent) >= 4.5 ? Colors.White : Colors.Black;
    }
}
