using System.Windows;

namespace Nepply.Modules.Appearance;

/// <summary>Inherited presentation hint; no settings, sampling or service ownership.</summary>
public static class PresentationDensity
{
    public static readonly DependencyProperty CompactProperty = DependencyProperty.RegisterAttached(
        "Compact", typeof(bool), typeof(PresentationDensity),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetCompact(DependencyObject element) => (bool)element.GetValue(CompactProperty);
    public static void SetCompact(DependencyObject element, bool value) => element.SetValue(CompactProperty, value);
}
