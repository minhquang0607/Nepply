using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Nepply.Views;

public static class WidgetDrag
{
    private const string Format = "Nepply.Widget";
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(WidgetDrag), new PropertyMetadata(false, Changed));
    public static readonly RoutedEvent ReorderedEvent = EventManager.RegisterRoutedEvent("Reordered", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(WidgetDrag));
    public static bool GetEnabled(DependencyObject value) => (bool)value.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject value, bool enabled) => value.SetValue(EnabledProperty, enabled);
    private static readonly DependencyProperty StartProperty = DependencyProperty.RegisterAttached("Start", typeof(Point?), typeof(WidgetDrag));
    private static readonly DependencyProperty PreviousFocusableProperty = DependencyProperty.RegisterAttached("PreviousFocusable", typeof(bool), typeof(WidgetDrag));
    private static readonly DependencyProperty SessionProperty = DependencyProperty.RegisterAttached("Session", typeof(DragSession), typeof(WidgetDrag));
    private sealed class DragSession(Window owner, FrameworkElement source, Panel parent) : IDisposable
    {
        internal readonly FrameworkElement Source = source;
        internal readonly Panel Parent = parent;
        internal void Cancel(object? sender, EventArgs e) => Dispose();
        public void Dispose()
        {
            Source.Unloaded -= Cancel;
            owner.Closed -= Cancel;
            if (ReferenceEquals(owner.GetValue(SessionProperty), this)) owner.ClearValue(SessionProperty);
        }
    }
    internal static bool WithDragSession(FrameworkElement source, Action drag)
    {
        if (!GetEnabled(source) || source.Parent is not Panel parent || Window.GetWindow(source) is not { } owner
            || owner.GetValue(SessionProperty) != null) return false;
        using var session = new DragSession(owner, source, parent);
        owner.SetValue(SessionProperty, session);
        source.Unloaded += session.Cancel;
        owner.Closed += session.Cancel;
        drag(); // Includes the nested OLE message loop; using clears state on cancel or exception.
        return true;
    }
    internal static FrameworkElement? InternalSource(FrameworkElement target)
    {
        if (Window.GetWindow(target)?.GetValue(SessionProperty) is not DragSession session) return null;
        var source = session.Source;
        return source != target && GetEnabled(source) && GetEnabled(target) && source.IsEnabled && target.IsEnabled
            && source.Visibility == Visibility.Visible && target.Visibility == Visibility.Visible
            && source.Parent == session.Parent && target.Parent == session.Parent ? source : null;
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        element.PreviewMouseLeftButtonDown -= Down; element.PreviewMouseMove -= Move; element.Drop -= Drop; element.DragOver -= Over;
        element.DragLeave -= Leave;
        element.PreviewKeyDown -= KeyDown;
        if ((bool)e.NewValue)
        {
            element.SetValue(PreviousFocusableProperty, element.Focusable);
            element.SetCurrentValue(UIElement.FocusableProperty, true);
        }
        else
        {
            element.SetCurrentValue(UIElement.FocusableProperty, (bool)element.GetValue(PreviousFocusableProperty));
            element.ClearValue(StartProperty);
            if (Window.GetWindow(element)?.GetValue(SessionProperty) is DragSession session && session.Source == element) session.Dispose();
        }
        element.AllowDrop = (bool)e.NewValue; element.Cursor = (bool)e.NewValue ? Cursors.SizeAll : Cursors.Arrow;
        if ((bool)e.NewValue) { element.PreviewMouseLeftButtonDown += Down; element.PreviewMouseMove += Move; element.Drop += Drop; element.DragOver += Over; element.DragLeave += Leave; element.PreviewKeyDown += KeyDown; }
    }
    private static void KeyDown(object sender, KeyEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if (!element.IsKeyboardFocused || Keyboard.Modifiers != ModifierKeys.Control || e.Key is not (Key.Up or Key.Down)) return;
        MoveByKeyboard(element, e.Key == Key.Up ? -1 : 1);
        e.Handled = true;
    }
    internal static bool MoveByKeyboard(FrameworkElement source, int direction)
    {
        if (!GetEnabled(source) || source.Parent is not Panel panel || direction is not (-1 or 1)) return false;
        int start = panel.Children.IndexOf(source), next = start + direction;
        while (next >= 0 && next < panel.Children.Count && panel.Children[next].Visibility != Visibility.Visible) next += direction;
        if (next < 0 || next >= panel.Children.Count) return false;
        panel.Children.RemoveAt(start); panel.Children.Insert(next, source);
        source.Focus(); source.BringIntoView();
        source.RaiseEvent(new RoutedEventArgs(ReorderedEvent, source));
        return true;
    }
    internal static bool CanStartFrom(FrameworkElement root, DependencyObject? source)
    {
        while (source != null && source != root)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.RangeBase
                or System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.Primitives.Selector) return false;
            source = source is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return source == root;
    }
    private static void Down(object sender, MouseButtonEventArgs e)
    {
        var element = (FrameworkElement)sender;
        element.ClearValue(StartProperty);
        if (CanStartFrom(element, e.OriginalSource as DependencyObject)) element.SetValue(StartProperty, e.GetPosition(element));
    }
    private static void Move(object sender, MouseEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if (e.LeftButton != MouseButtonState.Pressed) { element.ClearValue(StartProperty); return; }
        if (element.GetValue(StartProperty) is not Point start || (e.GetPosition(element) - start).Length < 7) return;
        element.ClearValue(StartProperty);
        var main = Window.GetWindow(element) as MainWindow;
        double opacity = element.Opacity;
        main?.SuppressHide();
        try { WithDragSession(element, () => { element.SetCurrentValue(UIElement.OpacityProperty, .55); DragDrop.DoDragDrop(element, new DataObject(Format, "internal"), DragDropEffects.Move); }); }
        finally { element.SetCurrentValue(UIElement.OpacityProperty, opacity); DropHint.Clear(); main?.ResumeHide(); }
        e.Handled = true;
    }
    private static void Over(object sender, DragEventArgs e)
    {
        var target = (FrameworkElement)sender;
        e.Effects = (e.AllowedEffects & DragDropEffects.Move) != 0 && InternalSource(target) != null ? DragDropEffects.Move : DragDropEffects.None;
        if (e.Effects == DragDropEffects.Move) DropHint.Show(target, e.GetPosition(target).Y >= target.ActualHeight / 2, false); else DropHint.Clear();
        e.Handled = true;
    }
    private static void Drop(object sender, DragEventArgs e)
    {
        DropHint.Clear();
        var target = (FrameworkElement)sender;
        e.Effects = DragDropEffects.None;
        if ((e.AllowedEffects & DragDropEffects.Move) != 0 && InternalSource(target) is { } source && source.Parent is Panel panel)
        {
            bool after = e.GetPosition(target).Y >= target.ActualHeight / 2;
            ((DragSession)Window.GetWindow(target).GetValue(SessionProperty)).Dispose();
            panel.Children.Remove(source); int index = panel.Children.IndexOf(target) + (after ? 1 : 0); panel.Children.Insert(index, source);
            e.Effects = DragDropEffects.Move;
            target.RaiseEvent(new RoutedEventArgs(ReorderedEvent, target));
        }
        e.Handled = true;
    }
    private static void Leave(object sender, DragEventArgs e) => DropHint.Clear();
}
