namespace Nepply.Modules.Desktop;

internal static class GridCapacity
{
    // Safety bounds, not the dimensions of the picker. All measurements are DIPs.
    internal const int MaximumColumns=40, MaximumRows=24;
    internal const double MinimumWidth=64, MinimumHeight=78;
    internal static (int Columns,int Rows) For(double width,double height)
        => (Count(width,MinimumWidth,MaximumColumns),Count(height,MinimumHeight,MaximumRows));
    internal static int Count(double extent,double cell,int limit)
        => double.IsFinite(extent) && extent>0 ? (int)Math.Clamp(Math.Floor(extent/cell),1,limit) : 1;
}
