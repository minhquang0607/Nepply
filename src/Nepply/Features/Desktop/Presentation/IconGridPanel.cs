using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Nepply.Modules.Desktop;
namespace Nepply.Views;

/// <summary>Viewport-fitted columns × rows. Realize only the viewport plus one row.</summary>
public sealed class IconGridPanel : VirtualizingPanel, IScrollInfo
{
    private readonly GridReflow _reflow = new();
    private double _lastArrangedOffset;
    private object? _scrollAnchor;
    private double _anchorFraction;
    private int _previewSource = -1, _previewInsertion = -1;
    internal void SetDropPreview(int source, int insertion)
    {
        if (_previewSource == source && _previewInsertion == insertion) return;
        _previewSource = source; _previewInsertion = insertion; InvalidateArrange();
    }
    internal void ClearDropPreview() => SetDropPreview(-1, -1);
    internal Rect SlotBounds(int index)
    {
        var position = _geometry.Position(index, VerticalOffset);
        return new Rect(position.X, position.Y, _geometry.TileWidth, _geometry.TileHeight);
    }
    internal static int PreviewIndex(int index, int source, int insertion, int count)
    {
        if (source < 0 || source >= count || insertion < 0 || insertion > count) return index;
        int target = insertion > source ? insertion - 1 : insertion;
        if (index == source) return target;
        if (source < target && index > source && index <= target) return index - 1;
        if (source > target && index >= target && index < source) return index + 1;
        return index;
    }
    public static readonly DependencyProperty MotionEnabledProperty = DependencyProperty.Register(nameof(MotionEnabled), typeof(bool), typeof(IconGridPanel), new PropertyMetadata(true, (d, e) => { if (!(bool)e.NewValue) ((IconGridPanel)d)._reflow.Clear(); }));
    public bool MotionEnabled { get => (bool)GetValue(MotionEnabledProperty); set => SetValue(MotionEnabledProperty, value); }
    public IconGridPanel()
    {
        Unloaded += (_, _) => { _reflow.Clear(); ClearDropPreview(); };
        IsVisibleChanged += (_, e) => { if (!(bool)e.NewValue) { _reflow.Clear(); ClearDropPreview(); } };
    }
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int), typeof(IconGridPanel), new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure), v => v is int n && n >= 1 && n <= GridCapacity.MaximumColumns);
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(int), typeof(IconGridPanel), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsMeasure), v => v is int n && n >= 1 && n <= GridCapacity.MaximumRows);
    public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public int Rows { get => (int)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    internal double RowHeight => _geometry.TileHeight;
    public static readonly DependencyProperty ContentScaleProperty = DependencyProperty.Register(nameof(ContentScale), typeof(double), typeof(IconGridPanel), new PropertyMetadata(1d));
    public double ContentScale { get => (double)GetValue(ContentScaleProperty); private set => SetValue(ContentScaleProperty, value); }
    private IconGridGeometry _geometry = IconGridGeometry.Fixed(368, 520, 4, 5);
    internal int ColumnCount => _geometry.Columns;
    private double _tileWidth => _geometry.TileWidth;
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public double ExtentWidth { get; private set; }
    public double ExtentHeight { get; private set; }
    public double ViewportWidth { get; private set; }
    public double ViewportHeight { get; private set; }
    public double HorizontalOffset => 0;
    public double VerticalOffset { get; private set; }
    public ScrollViewer? ScrollOwner { get; set; }
    internal int InsertionIndex(Point point, int itemCount)
    {
        if (point.X < 0 || point.Y < 0 || point.X > ViewportWidth || point.Y > ViewportHeight) return -1;
        return _geometry.Insertion(point.X, point.Y + VerticalOffset, itemCount);
    }
    internal static int CalculateInsertion(double x, double contentY, int count, int columns, double width, double height, double gap)
        => IconGridGeometry.CalculateInsertion(x, contentY, count, columns, width, height, gap, gap);
    protected override Size MeasureOverride(Size available)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner == null) return new();
        ViewportWidth = double.IsFinite(available.Width) ? available.Width : 88 * Columns;
        ViewportHeight = double.IsFinite(available.Height) ? available.Height : 90 * Rows;
        var previous = _geometry;
        _geometry = IconGridGeometry.Fit(ViewportWidth, ViewportHeight, Columns, Rows);
        ContentScale = Math.Clamp(Math.Min(_geometry.TileWidth / 80, _geometry.TileHeight / 86), .91, 1);
        if (previous.Columns != _geometry.Columns || previous.TileHeight != _geometry.TileHeight)
        {
            int anchor = (int)(VerticalOffset / previous.TileHeight) * previous.Columns;
            double fraction = (VerticalOffset % previous.TileHeight) / previous.TileHeight;
            VerticalOffset = (anchor / _geometry.Columns + fraction) * _geometry.TileHeight;
        }
        ExtentWidth = ViewportWidth; ExtentHeight = Math.Ceiling(owner.Items.Count / (double)ColumnCount) * RowHeight;
        VerticalOffset = Math.Clamp(VerticalOffset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        ScrollOwner?.InvalidateScrollInfo();
        int first = Math.Max(0, (int)(VerticalOffset / RowHeight) * ColumnCount);
        _scrollAnchor = first < owner.Items.Count ? owner.Items[first] : null;
        _anchorFraction = VerticalOffset % RowHeight / RowHeight;
        int last = Math.Min(owner.Items.Count - 1, ((int)((VerticalOffset + ViewportHeight) / RowHeight) + 2) * ColumnCount - 1);
        // Accessing InternalChildren initializes Panel's generator connection.
        var children = InternalChildren;
        var generator = ItemContainerGenerator;
        // Remove offscreen containers BEFORE generation, keeping generated positions stable.
        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            int index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index >= first && index <= last) continue;
            generator.Remove(new GeneratorPosition(i, 0), 1); RemoveInternalChildRange(i, 1);
        }
        var start = generator.GeneratorPositionFromIndex(first);
        int childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
        using (generator.StartAt(start, GeneratorDirection.Forward, true))
        {
            for (int index = first; index <= last; index++, childIndex++)
            {
                var child = (UIElement)generator.GenerateNext(out bool created);
                if (created)
                {
                    if (childIndex >= InternalChildren.Count) AddInternalChild(child); else InsertInternalChild(childIndex, child);
                    generator.PrepareItemContainer(child);
                }
                child.Measure(new Size(_tileWidth, RowHeight));
            }
        }
        return new Size(ViewportWidth, ViewportHeight);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        bool animate = MotionEnabled && IsVisible && IsLoaded && VerticalOffset == _lastArrangedOffset;
        var live = new HashSet<UIElement>();
        int itemCount = ItemsControl.GetItemsOwner(this)?.Items.Count ?? 0;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            int index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            index = PreviewIndex(index, _previewSource, _previewInsertion, itemCount);
            var child = InternalChildren[i]; live.Add(child);
            _reflow.Arrange(child, SlotBounds(index), animate);
        }
        _reflow.Retain(live); _lastArrangedOffset = VerticalOffset;
        return finalSize;
    }
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        if (args.ItemUICount > 0 && args.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Remove or System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
        if (args.ItemUICount > 0 && args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
            RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
        if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            RemoveInternalChildRange(0, InternalChildren.Count);
            var owner = ItemsControl.GetItemsOwner(this);
            int anchor = _scrollAnchor == null ? -1 : owner?.Items.IndexOf(_scrollAnchor) ?? -1;
            if (anchor >= 0) VerticalOffset = (anchor / ColumnCount + _anchorFraction) * RowHeight;
            // If the anchor disappeared, retain/clamp the offset at the next measure.
            _scrollAnchor = null;
            _reflow.Clear();
        }
        InvalidateMeasure();
    }
    protected override void BringIndexIntoView(int index)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner == null || index < 0 || index >= owner.Items.Count) return;
        double top = index / ColumnCount * RowHeight;
        SetVerticalOffset(OffsetToReveal(top, RowHeight, VerticalOffset, ViewportHeight));
    }
    internal static double OffsetToReveal(double top, double height, double offset, double viewport)
    {
        if (!double.IsFinite(top) || !double.IsFinite(height) || !double.IsFinite(offset)
            || !double.IsFinite(viewport) || top < 0 || height <= 0 || viewport <= 0) return offset;
        // A row taller than the viewport cannot be fully visible. Always align
        // its top instead of alternating between its top and bottom on focus.
        if (height >= viewport || top < offset) return top;
        if (top + height > offset + viewport) return top + height - viewport;
        return offset;
    }
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual == null || rectangle.IsEmpty || !IsAncestorOf(visual)
            || !double.IsFinite(rectangle.X) || !double.IsFinite(rectangle.Y)
            || !double.IsFinite(rectangle.Width) || !double.IsFinite(rectangle.Height)) return Rect.Empty;
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner == null) return Rect.Empty;
        var container = ItemsControl.ContainerFromElement(owner, visual);
        if (container == null) return Rect.Empty;
        // Capture the current visual coordinates before requesting a new layout.
        // Return only the part that will remain visible after scrolling, in this
        // panel's coordinates (not the child visual's local coordinates).
        var visible = visual.TransformToAncestor(this).TransformBounds(rectangle);
        double previousOffset = VerticalOffset;
        BringIndexIntoView(owner.ItemContainerGenerator.IndexFromContainer(container));
        visible.Offset(0, previousOffset - VerticalOffset);
        visible.Intersect(new Rect(0, 0, ViewportWidth, ViewportHeight));
        return visible;
    }
    public void SetVerticalOffset(double offset)
    {
        if (double.IsNaN(offset)) return;
        double next = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        if (next == VerticalOffset) return;
        VerticalOffset = next; ScrollOwner?.InvalidateScrollInfo(); InvalidateMeasure();
    }
    public void SetHorizontalOffset(double offset) { }
    public void LineUp() => SetVerticalOffset(VerticalOffset - 32);
    public void LineDown() => SetVerticalOffset(VerticalOffset + 32);
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - RowHeight);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + RowHeight);
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void LineLeft() { }
    public void LineRight() { }
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void PageLeft() { }
    public void PageRight() { }
}
