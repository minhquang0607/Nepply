using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using Nepply.Modules.Appearance;
namespace Nepply.Views;

public static class MeterMotion
{
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached("Target", typeof(double), typeof(MeterMotion), new PropertyMetadata(0d, Changed));
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(MeterMotion), new PropertyMetadata(true,(d,e)=>{if(d is ProgressBar bar && !(bool)e.NewValue) Settle(bar);}));
    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached("Hooked",typeof(bool),typeof(MeterMotion),new PropertyMetadata(false));
    private static readonly DependencyProperty GenerationProperty = DependencyProperty.RegisterAttached("Generation",typeof(long),typeof(MeterMotion),new PropertyMetadata(0L));
    public static double GetTarget(DependencyObject value) => (double)value.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject value, double target) => value.SetValue(TargetProperty, target);
    public static bool GetEnabled(DependencyObject value)=>(bool)value.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject value,bool enabled)=>value.SetValue(EnabledProperty,enabled);
    private static long Next(ProgressBar bar)
    {
        long generation=unchecked((long)bar.GetValue(GenerationProperty)+1);
        bar.SetValue(GenerationProperty,generation);return generation;
    }
    private static void Settle(ProgressBar bar)
    {
        Next(bar);bar.BeginAnimation(RangeBase.ValueProperty,null);
        double value=GetTarget(bar);
        if(double.IsFinite(value)) bar.Value=Math.Clamp(value,bar.Minimum,bar.Maximum);
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if(sender is not ProgressBar bar || !double.IsFinite((double)args.NewValue))return;
        if(!(bool)bar.GetValue(HookedProperty))
        {
            bar.SetValue(HookedProperty,true);
            bar.Unloaded+=(_,_)=>Settle(bar);
            bar.IsVisibleChanged+=(_,_)=>Settle(bar);
        }
        double before=bar.Value,target=Math.Clamp((double)args.NewValue,bar.Minimum,bar.Maximum);
        long generation=Next(bar);
        bar.BeginAnimation(RangeBase.ValueProperty,null);bar.Value=target;
        if(!bar.IsVisible || !GetEnabled(bar) || !MotionPolicy.Enabled || Math.Abs(before-target)<.01)return;
        var animation=new DoubleAnimation(before,target,MotionPolicy.Duration(240))
        {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        animation.Completed+=(_,_)=>{if((long)bar.GetValue(GenerationProperty)==generation)bar.BeginAnimation(RangeBase.ValueProperty,null);};
        bar.BeginAnimation(RangeBase.ValueProperty,animation);
    }
}
