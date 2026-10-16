using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nepply.Views;

/// <summary>Short hover motion with no retained animation clock after settling.</summary>
public static class HoverLift
{
    private static readonly DependencyProperty GenerationProperty=DependencyProperty.RegisterAttached(
        "Generation",typeof(long),typeof(HoverLift),new PropertyMetadata(0L));
    private static long NextGeneration(FrameworkElement element)
    {
        long generation=unchecked((long)element.GetValue(GenerationProperty)+1);
        element.SetValue(GenerationProperty,generation);
        return generation;
    }
    public static readonly DependencyProperty DistanceProperty=DependencyProperty.RegisterAttached(
        "Distance",typeof(double),typeof(HoverLift),new PropertyMetadata(0d,DistanceChanged));
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached(
        "Enabled",typeof(bool),typeof(HoverLift),new PropertyMetadata(true,EnabledChanged));
    public static readonly DependencyProperty HoveredProperty=DependencyProperty.RegisterAttached(
        "Hovered",typeof(bool),typeof(HoverLift),new PropertyMetadata(false,(d,e)=>
        { if(d is FrameworkElement element) Move(element,(bool)e.NewValue); }));
    public static bool GetHovered(DependencyObject value)=>(bool)value.GetValue(HoveredProperty);
    public static void SetHovered(DependencyObject value,bool hovered)=>value.SetValue(HoveredProperty,hovered);
    public static double GetDistance(DependencyObject value)=>(double)value.GetValue(DistanceProperty);
    public static void SetDistance(DependencyObject value,double distance)=>value.SetValue(DistanceProperty,distance);
    public static bool GetEnabled(DependencyObject value)=>(bool)value.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject value,bool enabled)=>value.SetValue(EnabledProperty,enabled);
    private static void DistanceChanged(DependencyObject sender,DependencyPropertyChangedEventArgs e)
    {
        if(sender is not FrameworkElement element) return;
        element.Unloaded-=Unloaded; element.IsVisibleChanged-=VisibilityChanged;
        Reset(element);
        if((double)e.NewValue==0) return;
        element.Unloaded+=Unloaded; element.IsVisibleChanged+=VisibilityChanged;
    }
    private static void EnabledChanged(DependencyObject sender,DependencyPropertyChangedEventArgs e)
    { if(sender is FrameworkElement element && !(bool)e.NewValue) Reset(element); }
    private static void Unloaded(object sender,RoutedEventArgs e)=>Reset((FrameworkElement)sender);
    private static void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)
    { if(!(bool)e.NewValue) Reset((FrameworkElement)sender); }
    private static TranslateTransform Transform(FrameworkElement element)
    {
        if(element.RenderTransform is TranslateTransform existing && !existing.IsFrozen) return existing;
        var transform=new TranslateTransform(); element.RenderTransform=transform; return transform;
    }
    private static void Reset(FrameworkElement element)
    {
        NextGeneration(element);
        var transform=Transform(element);
        transform.BeginAnimation(TranslateTransform.YProperty,null); transform.Y=0;
    }
    private static void Move(FrameworkElement element,bool hovered)
    {
        if(!GetEnabled(element) || !element.IsVisible || !Nepply.Modules.Appearance.MotionPolicy.Enabled)
        { Reset(element); return; }
        var transform=Transform(element);
        double from=transform.Y, target=hovered ? -GetDistance(element) : 0;
        long generation=NextGeneration(element);
        transform.BeginAnimation(TranslateTransform.YProperty,null);
        transform.Y=target;
        if(from==target) return;
        var animation=new DoubleAnimation(from,target,Nepply.Modules.Appearance.MotionPolicy.Duration(hovered ? 160 : 200))
        { EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop };
        animation.Completed+=(_,_)=>
        {
            if((long)element.GetValue(GenerationProperty)!=generation) return;
            transform.BeginAnimation(TranslateTransform.YProperty,null);
        };
        transform.BeginAnimation(TranslateTransform.YProperty,animation);
    }
}
