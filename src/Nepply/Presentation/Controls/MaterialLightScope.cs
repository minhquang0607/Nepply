using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nepply.Views;

// One light field in monitor coordinates; cards crop it instead of stretching it.
internal sealed class MaterialLightScope(FrameworkElement owner) : IDisposable
{
    internal const double Period = 240;
    private readonly Dictionary<Border, (LinearGradientBrush Brush, bool Moving)> _cards = [];
    internal AnimationClock? Clock { get; private set; }
    private bool _disposed;
    private readonly TranslateTransform _sharedShift = new();
    internal MaterialPointerLight? Pointer { get; private set; }
    private TransformGroup? _movingField;
    internal void Attach(Border card, LinearGradientBrush brush, bool moving)
    {
        if (_disposed) return;
        if (_cards.Count == 0) owner.LayoutUpdated += UpdatePositions;
        if (moving && Pointer == null)
        {
            Pointer = new(owner, Period);
            _movingField = new TransformGroup();
            _movingField.Children.Add(_sharedShift);
            _movingField.Children.Add(Pointer.Transform);
        }
        brush.RelativeTransform = Transform.Identity;
        brush.MappingMode = BrushMappingMode.Absolute;
        brush.Transform = moving ? _movingField! : Transform.Identity;
        _cards[card] = (brush, moving);
        Position(card, brush);
        if (moving)
        {
            if (Clock == null)
            {
                var sweep = new DoubleAnimation(0, Period, MetallicSurface.SweepDuration) { RepeatBehavior = RepeatBehavior.Forever };
                Timeline.SetDesiredFrameRate(sweep, 60);
                Clock = (AnimationClock)sweep.CreateClock(true);
                _sharedShift.ApplyAnimationClock(TranslateTransform.XProperty, Clock);
                _sharedShift.ApplyAnimationClock(TranslateTransform.YProperty, Clock);
                Pointer?.BindSweep(Clock);
            }
        }
    }
    internal void Detach(Border card)
    {
        if (!_cards.Remove(card, out var entry)) return;
        if (!_cards.Values.Any(c => c.Moving))
        {
            _sharedShift.BeginAnimation(TranslateTransform.XProperty, null);
            _sharedShift.BeginAnimation(TranslateTransform.YProperty, null);
            Clock?.Controller?.Remove(); Clock = null;
            Pointer?.Dispose(); Pointer = null; _movingField = null;
        }
        if (_cards.Count == 0) owner.LayoutUpdated -= UpdatePositions;
    }
    private void UpdatePositions(object? sender, EventArgs e)
    {
        foreach (var (card, entry) in _cards) Position(card, entry.Brush);
    }
    private void Position(Border card, LinearGradientBrush brush)
    {
        if (!owner.IsAncestorOf(card)) return;
        var origin = card.TranslatePoint(new Point(), owner);
        var start = new Point(-origin.X, -origin.Y);
        if (brush.StartPoint != start) brush.StartPoint = start;
        var end = new Point(start.X + Period, start.Y + Period);
        if (brush.EndPoint != end) brush.EndPoint = end;
    }
    public void Dispose()
    {
        _disposed = true;
        foreach (var card in _cards.Keys.ToArray()) Detach(card);
    }
}
