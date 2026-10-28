using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nepply.Views;

// Directional diagonal light; event-driven, with no per-move animation clocks.
internal sealed class MaterialPointerLight : IDisposable
{
    private readonly FrameworkElement _owner;
    private long _lastMove;
    private bool _disposed;
    private AnimationClock? _sweep;
    private bool _tracking;
    internal void BindSweep(AnimationClock sweep)
    {
        _sweep = sweep;
        if (_owner.IsMouseOver) MoveTo(Mouse.GetPosition(_owner));
        else if (_tracking) _sweep.Controller?.Pause();
    }
    private Point? _previous;
    private readonly double? _period;
    internal TranslateTransform Transform { get; } = new();

    internal MaterialPointerLight(FrameworkElement owner, double? period = null)
    {
        _owner = owner;
        _period = period;
        owner.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(MouseMoved), handledEventsToo: true);
        owner.MouseEnter += MouseEntered;
        owner.MouseLeave += MouseLeft;
    }

    private void MouseMoved(object sender, MouseEventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        // Cap brush invalidations from high-rate mice to approximately 60 Hz.
        if (Stopwatch.GetElapsedTime(_lastMove, now).TotalMilliseconds < 16) return;
        _lastMove = now;
        MoveTo(e.GetPosition(_owner));
    }

    private void MouseLeft(object sender, MouseEventArgs e) => MoveTo(null);
    private void MouseEntered(object sender, MouseEventArgs e) => MoveTo(e.GetPosition(_owner));

    internal void MoveTo(Point? position)
    {
        if (_disposed) return;
        bool tracking = position is { } p && double.IsFinite(p.X) && double.IsFinite(p.Y);
        if (tracking != _tracking)
        {
            _tracking = tracking;
            if (tracking) _sweep?.Controller?.Pause();
            else _sweep?.Controller?.Resume();
        }
        if (tracking && position is { } point)
        {
            if (_previous is { } previous)
            {
                double delta = point.Y - previous.Y;
                if (Math.Abs(delta) < 1) return;
                // One scalar moves both axes. No animation allocation per mouse event.
                double offset = Transform.X + Math.Clamp(delta * .35, -8, 8);
                double w = _owner.ActualWidth, h = _owner.ActualHeight;
                double period = _period ?? (w > 0 && h > 0 ? 2 / (1 / w + 1 / h) : 240);
                offset %= period;
                Transform.X = Transform.Y = offset;
            }
            _previous = point;
        }
        else _previous = null;
        // Preserve the phase when leaving; never spring back to the entry position.
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sweep = null;
        _owner.RemoveHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(MouseMoved));
        _owner.MouseEnter -= MouseEntered;
        _owner.MouseLeave -= MouseLeft;
        _previous = null;
        Transform.X = Transform.Y = 0;
    }
}
