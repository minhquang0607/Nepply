using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Nepply.Views;

/// <summary>Host-specific blank-space policy, including Nepply custom inputs.</summary>
internal static class SearchHitTest
{
    internal static bool IsBlank(DependencyObject? source, DependencyObject root)
    {
        for (var node = source; node != null; node = Parent(node))
        {
            if (node == root) return true;
            if (node is ButtonBase or TextBoxBase or ComboBox or RangeBase or Thumb
                or ListBoxItem or MenuItem or ColorField or DotGridPicker or System.Windows.Documents.Hyperlink) return false;
        }
        return false;
    }

    private static DependencyObject? Parent(DependencyObject node) => node is Visual or System.Windows.Media.Media3D.Visual3D
        ? VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node)
        : LogicalTreeHelper.GetParent(node);
}
