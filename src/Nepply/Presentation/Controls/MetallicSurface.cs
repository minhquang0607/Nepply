using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
namespace Nepply.Views;

public static class MetallicSurface
{
    public static bool MotionEnabled { get; set; } = true;
    public static bool MaterialEnabled { get; internal set; } = true;
    public static bool HighContrast { get; internal set; }
    public static bool NeonPalette { get; internal set; }
    public static bool NeonEnabled { get; internal set; } = true;
    public static readonly DependencyProperty AnimateProperty = DependencyProperty.RegisterAttached("Animate", typeof(bool), typeof(MetallicSurface), new PropertyMetadata(true, Changed));
    public static bool GetAnimate(DependencyObject element) => (bool)element.GetValue(AnimateProperty);
    public static void SetAnimate(DependencyObject element, bool value) => element.SetValue(AnimateProperty, value);
    public static readonly DependencyProperty FrameProperty = DependencyProperty.RegisterAttached("Frame", typeof(bool), typeof(MetallicSurface), new PropertyMetadata(true, Changed));
    public static bool GetFrame(DependencyObject element) => (bool)element.GetValue(FrameProperty);
    public static void SetFrame(DependencyObject element, bool value) => element.SetValue(FrameProperty, value);
    public static double GlowStrength { get; set; } = .22;
    public static readonly DependencyProperty AccentProperty = DependencyProperty.RegisterAttached("Accent", typeof(Brush), typeof(MetallicSurface), new PropertyMetadata(null, Changed));
    public static Brush? GetAccent(DependencyObject element) => (Brush?)element.GetValue(AccentProperty);
    public static void SetAccent(DependencyObject element, Brush value) => element.SetValue(AccentProperty, value);
    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached("Hooked", typeof(bool), typeof(MetallicSurface), new PropertyMetadata(false));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(SurfaceState), typeof(MetallicSurface));
    internal static readonly DependencyProperty LightScopeProperty = DependencyProperty.RegisterAttached("LightScope", typeof(MaterialLightScope), typeof(MetallicSurface), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, Changed));
    private static readonly DependencyProperty ActiveScopeProperty = DependencyProperty.RegisterAttached("ActiveScope", typeof(MaterialLightScope), typeof(MetallicSurface));
    private static readonly DependencyProperty PointerProperty = DependencyProperty.RegisterAttached("Pointer", typeof(MaterialPointerLight), typeof(MetallicSurface));
    private sealed record SurfaceState(Color Accent, Color Base, bool Metallic, bool Neon, bool Motion, double Glow, MaterialLightScope? Scope, bool Ready, bool Frame);
    internal static readonly TimeSpan SweepDuration = TimeSpan.FromSeconds(22 / 1.05);
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Border border) return;
        if (e.Property == LightScopeProperty && GetAccent(border) == null) return;
        if (!(bool)border.GetValue(HookedProperty))
        {
            border.SetValue(HookedProperty, true);
            border.Loaded += Refresh; border.Unloaded += Stop;
            border.IsVisibleChanged += (_, _) => { if (border.IsVisible) Apply(border); else Halt(border); };
        }
        Apply(border);
    }
    private static void Refresh(object sender, RoutedEventArgs e) => Apply((Border)sender);
    private static void Stop(object sender, RoutedEventArgs e) => Halt((Border)sender);
    private static void Halt(Border border)
    {
        if (border.GetValue(ActiveScopeProperty) is MaterialLightScope scope) scope.Detach(border);
        border.ClearValue(ActiveScopeProperty);
        if (border.GetValue(PointerProperty) is MaterialPointerLight pointer) pointer.Dispose();
        border.ClearValue(PointerProperty);
        if (border.Background is LinearGradientBrush { RelativeTransform: TranslateTransform transform })
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
        }
        border.ClearValue(StateProperty);
    }
    private static void Apply(Border border)
    {
        if (HighContrast)
        {
            Halt(border);
            border.Background = SystemColors.WindowBrush;
            border.BorderBrush = SystemColors.WindowTextBrush;
            border.BorderThickness = new(1);
            border.Effect = null;
            return;
        }
        if (GetAccent(border) is not SolidColorBrush accent) return;
        var color = accent.Color;
        var baseColor = (Application.Current.Resources["CardBrush"] as SolidColorBrush)?.Color ?? Color.FromRgb(29, 32, 36);
        double glow = NeonEnabled ? (double.IsFinite(GlowStrength) ? Math.Clamp(GlowStrength, 0, .5) : .22) : 0;
        if (!GetFrame(border)) glow = 0;
        if (!GetAnimate(border)) glow *= .65;
        bool animate = GetAnimate(border) && MotionEnabled && MaterialEnabled && border.IsVisible && border.IsLoaded;
        var lightScope = border.GetValue(LightScopeProperty) as MaterialLightScope;
        var state = new SurfaceState(color, baseColor, MaterialEnabled, NeonPalette, animate, glow, lightScope, border.IsLoaded && border.IsVisible, GetFrame(border));
        // Resource refreshes with identical settings must not restart an in-flight sweep.
        if (Equals(border.GetValue(StateProperty), state)) return;
        Halt(border);
        border.SetValue(StateProperty, state);
        Color Blend(double amount) => Color.FromRgb((byte)(baseColor.R * (1 - amount) + color.R * amount), (byte)(baseColor.G * (1 - amount) + color.G * amount), (byte)(baseColor.B * (1 - amount) + color.B * amount));
        // Repeating diagonal bands have matching endpoints: each cycle advances one full period.
        var material = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1), SpreadMethod = GradientSpreadMethod.Repeat };
        double peak = NeonPalette ? .18 : .24;
        if (!GetAnimate(border)) peak *= .65;
        material.GradientStops = !MaterialEnabled && NeonPalette
            ? new GradientStopCollection { new(Blend(.16), 0), new(Blend(.04), .5), new(Blend(.16), 1) }
            : new GradientStopCollection(Enumerable.Range(0, 17).Select(i =>
            {
                double position = i / 16d;
                // A smooth periodic lobe: equal endpoints and a flat slope at the seam.
                double light = Math.Pow((1 - Math.Cos(Math.Min(position, 1 - position) * 2 * Math.PI)) / 2, 3);
                return new GradientStop(Blend(.035 + (peak - .035) * light), position);
            }));
        if (!MaterialEnabled && !NeonPalette)
            material.GradientStops = new GradientStopCollection { new(baseColor, 0), new(baseColor, 1) };
        var shift = new TranslateTransform(); material.RelativeTransform = shift;
        border.Background = material;
        if (GetFrame(border))
        {
            border.BorderThickness = new(NeonPalette ? 1 : .7);
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(NeonPalette ? (byte)170 : (byte)70, color.R, color.G, color.B));
        }
        else
        {
            // Section backgrounds can share moving light without a decorative metallic rim.
            border.ClearValue(Border.BorderThicknessProperty);
            border.ClearValue(Border.BorderBrushProperty);
        }
        border.Effect = glow <= 0 ? null : new DropShadowEffect { Color = color, ShadowDepth = 0, BlurRadius = NeonPalette ? 18 : 10, Opacity = glow * (NeonPalette ? 1.6 : .35), RenderingBias = RenderingBias.Performance };
        if (lightScope != null && border.IsLoaded && border.IsVisible)
        {
            border.SetValue(ActiveScopeProperty, lightScope);
            lightScope.Attach(border, material, animate);
        }
        else if (animate)
        {
            var pointer = new MaterialPointerLight(border);
            border.SetValue(PointerProperty, pointer);
            material.Transform = pointer.Transform;
            // A (1,1) translation follows the gradient vector by exactly one period,
            // including wide GPU cards. X-only translation was seamless only on squares.
            var animation = new DoubleAnimation(0, 1, SweepDuration) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(animation, 60);
            var clock = (AnimationClock)animation.CreateClock(true);
            shift.ApplyAnimationClock(TranslateTransform.XProperty, clock);
            shift.ApplyAnimationClock(TranslateTransform.YProperty, clock);
            pointer.BindSweep(clock);
        }
    }
}
