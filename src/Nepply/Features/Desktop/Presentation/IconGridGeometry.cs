namespace Nepply.Modules.Desktop;

/// <summary>One layout snapshot for arrangement, drop hints and insertion. Units are DIPs.</summary>
internal readonly record struct IconGridGeometry(int Columns, double TileWidth, double TileHeight, double Gap, double OriginX)
{
    internal static IconGridGeometry Create(double viewportWidth, double minimumWidth, double tileHeight)
    {
        if (!double.IsFinite(viewportWidth) || viewportWidth < 0
            || !double.IsFinite(minimumWidth) || minimumWidth <= 0
            || !double.IsFinite(tileHeight) || tileHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        int columns = (int)Math.Clamp(Math.Floor(viewportWidth / minimumWidth), 1, int.MaxValue);
        double width = Math.Min(minimumWidth, viewportWidth);
        double remaining = Math.Max(0, viewportWidth - columns * width);
        double gap = Math.Min(12, remaining / (columns + 1));
        double origin = (viewportWidth - columns * width - (columns - 1) * gap) / 2;
        return new(columns, width, tileHeight, gap, origin);
    }

    internal static IconGridGeometry Fixed(double width,double height,int columns,int rows)
    {
        if(!double.IsFinite(width) || !double.IsFinite(height) || width<0 || height<0) throw new ArgumentOutOfRangeException(nameof(width));
        columns=Math.Clamp(columns,1,GridCapacity.MaximumColumns); rows=Math.Clamp(rows,1,GridCapacity.MaximumRows);
        return new(columns,Math.Max(.01,width/columns),Math.Max(.01,height/rows),0,0);
    }

    internal static IconGridGeometry Fit(double width,double height,int columns,int rows)
    {
        var capacity=GridCapacity.For(width,height);
        var fitted=Fixed(width,height,Math.Min(columns,capacity.Columns),Math.Min(rows,capacity.Rows));
        // A short viewport scrolls over a full-height row instead of crushing its contents.
        return fitted with {TileHeight=Math.Max(GridCapacity.MinimumHeight,fitted.TileHeight)};
    }

    internal (double X, double Y) Position(int index, double verticalOffset)
        => (OriginX + index % Columns * (TileWidth + Gap), index / Columns * TileHeight - verticalOffset);

    internal int Insertion(double x, double contentY, int count)
        => CalculateInsertion(x, contentY, count, Columns, TileWidth, TileHeight, Gap, OriginX);

    internal static int CalculateInsertion(double x, double contentY, int count, int columns,
        double width, double height, double gap, double origin)
    {
        if (count <= 0 || columns <= 0 || !double.IsFinite(x) || !double.IsFinite(contentY)
            || !double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(gap)
            || !double.IsFinite(origin) || width <= 0 || height <= 0 || gap < 0) return -1;
        double row = Math.Max(0, Math.Floor(contentY / height));
        double slot = Math.Clamp(Math.Floor((x - origin - width / 2) / (width + gap)) + 1, 0, columns);
        return (int)Math.Min(count, row * columns + slot);
    }
}
