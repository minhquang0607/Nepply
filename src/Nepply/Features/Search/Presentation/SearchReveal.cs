using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nepply.Modules.Search;

/// <summary>Reveal text at its normal size, starting at the search button.</summary>
internal sealed class SearchReveal
{
    private int _generation;
    private bool _expanded;
    internal void Settle(FrameworkElement editor, bool expanded)
    {
        ++_generation;
        _expanded = expanded;
        Reset(editor, expanded);
    }
    internal void Set(FrameworkElement editor, FrameworkElement anchor, bool expanded)
    {
        if(_expanded==expanded) return;
        _expanded=expanded;
        int generation=++_generation;
        editor.Visibility=Visibility.Visible;
        editor.UpdateLayout();
        double width=editor.ActualWidth, height=editor.ActualHeight;
        if(!editor.IsLoaded || width<=0 || height<=0 || !Nepply.Modules.Appearance.MotionPolicy.Enabled)
        { Reset(editor,expanded); return; }
        var point=anchor.TranslatePoint(new Point(anchor.ActualWidth/2,anchor.ActualHeight/2),editor);
        double diameter=Math.Min(32,height);
        double startX=Math.Clamp(point.X-diameter/2,0,Math.Max(0,width-diameter));
        var small=new Rect(startX,(height-diameter)/2,diameter,diameter);
        var full=new Rect(0,0,width,height);
        // Sample animated values before replacing clocks so rapid reversals stay continuous.
        var startRect=editor.Clip is RectangleGeometry activeClip ? activeClip.Rect : expanded ? small : full;
        var activeOffset=editor.RenderTransform as TranslateTransform;
        double? startOffsetX=activeOffset?.X, startOffsetY=activeOffset?.Y;
        var clip=new RectangleGeometry(startRect,20,20);
        editor.Clip=clip;
        var offset=new TranslateTransform(); editor.RenderTransform=offset;
        double dx=point.X-(startX+diameter/2),dy=point.Y-height/2;
        var duration=Nepply.Modules.Appearance.MotionPolicy.Duration(expanded ? 220 : 160);
        var ease=new CubicEase {EasingMode=EasingMode.EaseOut};
        offset.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(startOffsetX ?? (expanded ? dx : 0),expanded ? 0 : dx,duration){EasingFunction=ease});
        offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(startOffsetY ?? (expanded ? dy : 0),expanded ? 0 : dy,duration){EasingFunction=ease});
        var animation=new RectAnimation(startRect,expanded ? full : small,duration){EasingFunction=ease};
        animation.Completed+=(_,_)=> {if(generation==_generation) Reset(editor,expanded);};
        clip.BeginAnimation(RectangleGeometry.RectProperty,animation);
    }
    private static void Reset(FrameworkElement editor,bool expanded)
    {
        if(editor.Clip is RectangleGeometry clip) clip.BeginAnimation(RectangleGeometry.RectProperty,null);
        if(editor.RenderTransform is TranslateTransform offset) {offset.BeginAnimation(TranslateTransform.XProperty,null);offset.BeginAnimation(TranslateTransform.YProperty,null);}
        editor.Clip=null; editor.RenderTransform=Transform.Identity;
        editor.Visibility=expanded ? Visibility.Visible : Visibility.Collapsed;
    }
}
