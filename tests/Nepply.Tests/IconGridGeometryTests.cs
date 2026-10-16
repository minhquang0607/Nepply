using Nepply.Modules.Desktop;

internal static class IconGridGeometryTests
{
    internal static void Run(Action<string, bool> check)
    {
        foreach (double viewport in new[] { 1d, 71, 72, 143.5, 144, 239, 320, 479.5, 1000 })
        {
            var grid = IconGridGeometry.Create(viewport, 72, 90);
            var first = grid.Position(0, 0);
            var last = grid.Position(grid.Columns - 1, 0);
            check($"Grid {viewport} centers bounded gaps", grid.Gap >= 0 && grid.Gap <= 12
                && first.X >= 0 && Math.Abs(first.X - (viewport - last.X - grid.TileWidth)) < .00001);
            bool aligned = true;
            for (int index = 0; index < grid.Columns * 3; index++)
            {
                var slot = grid.Position(index, 45);
                double midpoint = slot.X + grid.TileWidth / 2;
                aligned &= grid.Insertion(midpoint - .001, slot.Y + 45 + 1, grid.Columns * 3) == index;
                aligned &= grid.Insertion(midpoint + .001, slot.Y + 45 + 1, grid.Columns * 3) == index + 1;
            }
            check($"Grid {viewport} drop boundary matches every arranged midpoint after scroll", aligned);
        }
        var capped = IconGridGeometry.Create(239, 120, 90);
        check("Wide single column uses outer margins instead of oversized gap", capped.Gap == 12 && capped.OriginX == 59.5);
        var zero = IconGridGeometry.Create(0, 72, 90);
        check("Zero viewport has safe geometry and no insertion", zero.Columns == 1 && zero.TileWidth == 0 && zero.Insertion(0, 0, 3) == -1);
        var partial = IconGridGeometry.Create(300, 72, 90);
        check("Partial final row and blank trailing area clamp to item count", partial.Insertion(300, 91, 5) == 5 && partial.Insertion(20, 999, 5) == 5);
        bool rejected = false;
        try { IconGridGeometry.Create(double.NaN, 72, 90); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check("Grid rejects nonfinite viewport", rejected);
    }
}
