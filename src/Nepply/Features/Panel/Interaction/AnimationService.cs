using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Nepply.Models;
namespace Nepply.Services;

public sealed class AnimationService(TranslateTransform transform, Window window, AppSettings settings, FrameworkElement? surface = null)
{
    private int _generation;
    internal Func<bool> AnimationsEnabled { get; set; } = () => Nepply.Modules.Appearance.MotionPolicy.Enabled;
    public bool IsVisible { get; private set; }
    public bool IsAnimating { get; private set; }
    public event Action<bool>? VisibilityChanged;
    public event Action<bool>? Settled;
    public void Show() => Transition(true);
    public void Hide() => Transition(false);
    // Re-anchor an open dock without hiding its HWND or losing keyboard focus.
    public void RefreshGeometry() => Settle(IsVisible);
    public void RefreshMotionPreference()
    {
        if (IsAnimating && !AnimationsEnabled()) Settle(IsVisible);
    }
    private void Settle(bool visible)
    {
        ++_generation;
        ClearMorph();
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.X = !visible && settings.EdgePosition == EdgePosition.Left ? -window.Width
            : !visible && settings.EdgePosition == EdgePosition.Right ? window.Width : 0;
        transform.Y = !visible && settings.EdgePosition == EdgePosition.Top ? -window.Height
            : !visible && settings.EdgePosition == EdgePosition.Bottom ? window.Height : 0;
        IsAnimating = false;
        if (!visible) window.Hide();
        Settled?.Invoke(visible);
    }
    public void HideInstantly()
    {
        IsVisible = false;
        Settle(false); VisibilityChanged?.Invoke(false);
    }
    private void Transition(bool visible)
    {
        if (IsVisible == visible) return;
        IsVisible = visible; IsAnimating = true;
        int generation = ++_generation;
        if (visible) window.Show();
        if (generation != _generation) return;
        VisibilityChanged?.Invoke(visible);
        if (!AnimationsEnabled()) { Settle(visible); return; }
        var property = settings.EdgePosition.IsHorizontal() ? TranslateTransform.YProperty : TranslateTransform.XProperty;
        double current = (double)transform.GetValue(property);
        double target = visible ? 0 : settings.EdgePosition switch { EdgePosition.Left => -window.Width, EdgePosition.Right => window.Width, EdgePosition.Bottom => window.Height, _ => -window.Height };
        transform.BeginAnimation(property, null); transform.SetValue(property, current);
        var animation = new DoubleAnimation(current, target, Nepply.Modules.Appearance.MotionPolicy.Duration(visible ? 280 : 190))
        { EasingFunction = new CubicEase { EasingMode = visible ? EasingMode.EaseOut : EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) =>
        {
            if (generation != _generation) return;
            transform.BeginAnimation(property, null); transform.SetValue(property, target); IsAnimating = false;
            if (!visible) window.Hide();
            Settled?.Invoke(visible);
        };
        transform.BeginAnimation(property, animation);
    }
    private void ClearMorph()
    {
        if (surface?.Clip is RectangleGeometry geometry) geometry.BeginAnimation(RectangleGeometry.RectProperty, null);
        if (surface != null) surface.Clip = null;
    }
}
