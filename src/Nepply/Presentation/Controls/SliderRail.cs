using System.Windows;
using System.Windows.Media;
namespace Nepply.Views;

// One continuous rail aligned with the native Track's 28-DIP thumb center.
public sealed class SliderRail : FrameworkElement
{
    private static DependencyProperty Number(string name, double value) => DependencyProperty.Register(name, typeof(double), typeof(SliderRail), new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MinimumProperty = Number(nameof(Minimum), 0);
    public static readonly DependencyProperty MaximumProperty = Number(nameof(Maximum), 100);
    public static readonly DependencyProperty ValueProperty = Number(nameof(Value), 0);
    public static readonly DependencyProperty IsDirectionReversedProperty = DependencyProperty.Register(nameof(IsDirectionReversed), typeof(bool), typeof(SliderRail), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(SliderRail), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(SliderRail), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty,value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty,value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty,value); }
    public bool IsDirectionReversed { get => (bool)GetValue(IsDirectionReversedProperty); set => SetValue(IsDirectionReversedProperty,value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty,value); }
    public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty,value); }
    protected override void OnRender(DrawingContext dc)
    {
        double inset=Math.Min(14,ActualWidth/2), length=Math.Max(0,ActualWidth-2*inset), y=ActualHeight/2;
        if(length<=0) return;
        dc.DrawLine(new Pen(Background,3) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(inset,y),new(ActualWidth-inset,y));
        double fraction=Maximum>Minimum ? Math.Clamp((Value-Minimum)/(Maximum-Minimum),0,1) : 0;
        if(!double.IsFinite(fraction) || fraction<=0) return;
        double start=IsDirectionReversed ? ActualWidth-inset : inset;
        double end=start+(IsDirectionReversed ? -1 : 1)*length*fraction;
        dc.DrawLine(new Pen(Foreground,3) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(start,y),new(end,y));
    }
}
