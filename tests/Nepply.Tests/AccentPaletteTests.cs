using Nepply.Modules.Appearance;
using System.Windows.Media;

internal static class AccentPaletteTests
{
    internal static void Run(Action<string, bool> check)
    {
        Color[] dark = [Color.FromRgb(20,22,25),Color.FromRgb(41,45,50)];
        Color[] light = [Colors.White,Color.FromRgb(233,230,224)];
        foreach (var surfaces in new[] { dark, light })
        {
            bool preserved = true, readable = true, label = true;
            for (int r = 0; r <= 255; r += 51)
            for (int g = 0; g <= 255; g += 51)
            for (int b = 0; b <= 255; b += 51)
            {
                var source = Color.FromRgb((byte)r,(byte)g,(byte)b);
                var palette = AccentPalette.Create(source, surfaces);
                preserved &= palette.Source == source;
                readable &= surfaces.All(surface => AccentPalette.Contrast(palette.Functional,surface) >= 4.5);
                label &= AccentPalette.Contrast(palette.Functional,palette.OnFunctional) >= 4.5;
            }
            string theme = ReferenceEquals(surfaces,dark) ? "dark" : "light";
            check($"Accent {theme} preserves all sampled user colors",preserved);
            check($"Accent {theme} adapts sampled colors for theme surfaces",readable);
            check($"Accent {theme} labels remain readable on accent fill",label);
        }
        var champagne = Color.FromRgb(217,188,139);
        check("Already readable accent is not altered",AccentPalette.Create(champagne,dark).Functional == champagne);
        bool rejected = false;
        try { AccentPalette.Create(Colors.Red,[]); } catch(ArgumentException) { rejected = true; }
        check("Color policy requires explicit surfaces",rejected);
    }
}
