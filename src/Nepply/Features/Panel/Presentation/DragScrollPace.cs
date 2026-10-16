namespace Nepply.Views;

/// <summary>Time-based edge scrolling; dense drag events do not increase speed.</summary>
internal sealed class DragScrollPace
{
    private long? _last;
    private int _direction;
    internal void Reset() { _last=null; _direction=0; }
    internal static int DirectionAt(double y,double height)
    {
        if(!double.IsFinite(y) || !double.IsFinite(height) || height<=0 || y<0 || y>height) return 0;
        double zone=Math.Min(36,height/3);
        return y<zone ? -1 : y>height-zone ? 1 : 0;
    }
    internal static bool CanScroll(double x,double y,double width,double height,double offset,double extent)
    {
        if (!double.IsFinite(x) || !double.IsFinite(width) || width<=0 || x<0 || x>width
            || !double.IsFinite(offset) || !double.IsFinite(extent) || offset<0 || extent<0) return false;
        return DirectionAt(y,height) switch
        {
            -1 => offset>0,
            1 => offset<Math.Max(0,extent-height),
            _ => false
        };
    }
    internal double Step(double y,double height,long nowMs)
    {
        int direction=DirectionAt(y,height);
        if(direction==0) { Reset(); return 0; }
        double zone=Math.Min(36,height/3);
        if(_last==null || _direction!=direction || nowMs<_last.Value)
        { _last=nowMs; _direction=direction; return 0; }
        double seconds=Math.Min(50,nowMs-_last.Value)/1000d;
        _last=nowMs;
        double proximity=direction<0 ? (zone-y)/zone : (y-height+zone)/zone;
        return direction*300*proximity*proximity*seconds;
    }
}
