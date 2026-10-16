using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Nepply.Views;

/// <summary>Non-hit-test overlay; never changes the layout or the drop target.</summary>
public sealed class DropHint : Adorner
{
    private static DropHint? _current;
    private readonly AdornerLayer _layer;
    private readonly bool _after, _horizontal;
    private readonly Rect? _slot;
    private DropHint(UIElement target, AdornerLayer layer, bool after, bool horizontal,Rect? slot) : base(target)
    { _layer=layer; _after = after; _horizontal = horizontal; _slot=slot; IsHitTestVisible = false; }
    public static void Show(UIElement target, bool after, bool horizontal,Rect? slot=null)
    {
        if (_current?.AdornedElement == target && _current._after == after && _current._horizontal == horizontal && _current._slot==slot) return;
        Clear();
        if (AdornerLayer.GetAdornerLayer(target) is not { } layer) return;
        var hint=new DropHint(target,layer,after,horizontal,slot);
        layer.Add(hint); _current=hint;
        target.IsVisibleChanged+=hint.TargetVisibilityChanged;
        if(target is FrameworkElement element) element.Unloaded+=hint.TargetUnloaded;
    }
    public static void Clear()
    {
        if (_current == null) return;
        var hint=_current; _current=null;
        hint.AdornedElement.IsVisibleChanged-=hint.TargetVisibilityChanged;
        if(hint.AdornedElement is FrameworkElement element) element.Unloaded-=hint.TargetUnloaded;
        hint._layer.Remove(hint);
    }
    private void TargetUnloaded(object sender,RoutedEventArgs e)
    { if(ReferenceEquals(_current,this)) Clear(); }
    private void TargetVisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)
    { if(!(bool)e.NewValue && ReferenceEquals(_current,this)) Clear(); }
    protected override void OnRender(DrawingContext dc)
    {
        var bounds=_slot ?? new Rect(AdornedElement.RenderSize);
        var size = bounds.Size;
        dc.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));
        dc.PushTransform(new TranslateTransform(bounds.X,bounds.Y));
        var brush = (Brush?)TryFindResource("AccentBrush") ?? Brushes.Cyan;
        dc.PushOpacity(.13); dc.DrawRoundedRectangle(brush, null, new Rect(size), 14, 14); dc.Pop();
        var pen = new Pen(brush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        Point start = _horizontal ? new(_after ? size.Width - 2 : 2, 6) : new(6, _after ? size.Height - 2 : 2);
        Point end = _horizontal ? new(start.X, Math.Max(6, size.Height - 6)) : new(Math.Max(6, size.Width - 6), start.Y);
        dc.DrawLine(pen, start, end); dc.DrawEllipse(brush, null, start, 4, 4); dc.DrawEllipse(brush, null, end, 4, 4);
        dc.Pop(); dc.Pop();
    }
}
