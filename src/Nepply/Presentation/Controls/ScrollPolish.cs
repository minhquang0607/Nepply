using System.Windows;
using System.Windows.Controls;
namespace Nepply.Views;
public static class ScrollPolish
{
    public static readonly DependencyProperty SuppressMenuProperty = DependencyProperty.RegisterAttached("SuppressMenu", typeof(bool), typeof(ScrollPolish), new PropertyMetadata(false, Changed));
    public static bool GetSuppressMenu(DependencyObject value) => (bool)value.GetValue(SuppressMenuProperty);
    public static void SetSuppressMenu(DependencyObject value, bool enabled) => value.SetValue(SuppressMenuProperty, enabled);
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is FrameworkElement element) { element.ContextMenuOpening -= Block; if ((bool)e.NewValue) element.ContextMenuOpening += Block; }
    }
    private static void Block(object sender, ContextMenuEventArgs e) => e.Handled = true;
}
