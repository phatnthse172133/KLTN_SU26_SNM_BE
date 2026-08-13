using System.Text.Json;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public LayoutRect Inflate(double padding)
        => new(X - padding, Y - padding, Width + padding * 2, Height + padding * 2);

    public bool Contains(double px, double py)
        => px >= Left && px <= Right && py >= Top && py <= Bottom;

    public bool Intersects(LayoutRect other)
        => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}

public sealed record BlockGeometry(
    double BoothWidth,
    double BoothHeight,
    double HorizontalGap,
    double VerticalGap,
    double InnerPadding,
    int Columns,
    int Rows,
    bool Physical);

public sealed record BoothObstacle(
    Guid NodeId,
    string? SlotCode,
    Guid? LayoutBlockId,
    Guid? ZoneId,
    int? RowIndex,
    int? ColumnIndex,
    LayoutRect Bounds);

public static class LayoutBlockGeometry
{
    public static BlockGeometry Read(LayoutBlock block)
    {
        var boothWidth = ReadNumber(block.ConfigJson, "boothWidth", 40);
        var boothHeight = ReadNumber(block.ConfigJson, "boothHeight", 40);
        return new BlockGeometry(
            boothWidth,
            boothHeight,
            ReadNumber(block.ConfigJson, "horizontalGap", 0),
            ReadNumber(block.ConfigJson, "verticalGap", 0),
            ReadNumber(block.ConfigJson, "innerPadding", 0),
            (int)Math.Max(1, ReadNumber(block.ConfigJson, "columns", 1)),
            (int)Math.Max(1, ReadNumber(block.ConfigJson, "rows", 1)),
            ReadBoolean(block.ConfigJson, "physical"));
    }

    public static LayoutRect BoothRect(LayoutNode slot, BlockGeometry geometry)
        => new(
            (double)slot.Xcoordinate - geometry.BoothWidth / 2,
            (double)slot.Ycoordinate - geometry.BoothHeight / 2,
            geometry.BoothWidth,
            geometry.BoothHeight);

    public static IReadOnlyList<BoothObstacle> ReconstructBooths(
        IReadOnlyCollection<LayoutBlock> blocks,
        IReadOnlyCollection<LayoutNode> nodes)
    {
        var geometryByBlock = blocks.ToDictionary(block => block.Id, Read);
        var obstacles = new List<BoothObstacle>();
        foreach (var slot in nodes.Where(node => !node.IsDeleted && node.NodeType == LayoutNodeType.BoothSlot))
        {
            BlockGeometry geometry;
            if (slot.LayoutBlockId is Guid blockId && geometryByBlock.TryGetValue(blockId, out var fromId))
                geometry = fromId;
            else
            {
                var block = blocks.FirstOrDefault(item => item.ZoneId.HasValue && item.ZoneId == slot.ZoneId);
                geometry = block is null ? new BlockGeometry(40, 40, 0, 0, 0, 1, 1, false) : Read(block);
            }

            obstacles.Add(new BoothObstacle(
                slot.Id, slot.SlotCode, slot.LayoutBlockId, slot.ZoneId,
                slot.RowIndex, slot.ColumnIndex, BoothRect(slot, geometry)));
        }

        return obstacles;
    }

    public static bool SegmentIntersects(double x1, double y1, double x2, double y2, LayoutRect rect)
    {
        if (rect.Contains(x1, y1) || rect.Contains(x2, y2))
            return true;
        return Crosses(x1, y1, x2, y2, rect.Left, rect.Top, rect.Right, rect.Top)
            || Crosses(x1, y1, x2, y2, rect.Right, rect.Top, rect.Right, rect.Bottom)
            || Crosses(x1, y1, x2, y2, rect.Right, rect.Bottom, rect.Left, rect.Bottom)
            || Crosses(x1, y1, x2, y2, rect.Left, rect.Bottom, rect.Left, rect.Top);
    }

    public static double ReadNumber(string? json, string property, double fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(property, out var value) && value.TryGetDouble(out var result)
                ? result
                : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static bool ReadBoolean(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(property, out var value)
                && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                && value.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Crosses(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy)
    {
        var d1 = Direction(cx, cy, dx, dy, ax, ay);
        var d2 = Direction(cx, cy, dx, dy, bx, by);
        var d3 = Direction(ax, ay, bx, by, cx, cy);
        var d4 = Direction(ax, ay, bx, by, dx, dy);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;
        return false;
    }

    private static double Direction(double ax, double ay, double bx, double by, double cx, double cy)
        => (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
}

public readonly record struct LayoutPhysicalScale(double ScaleX, double ScaleY, string Source)
{
    public bool IsUniform => Math.Abs(ScaleX - ScaleY) <= 1e-12;
}

public static class LayoutPhysicalCalibration
{
    public static LayoutPhysicalScale? TryResolve(MarketLayout layout, NightMarket? market = null)
    {
        if (layout.Width > 0 && layout.Height > 0)
        {
            if (layout.MarketWidthMeters is > 0 && layout.MarketLengthMeters is > 0)
                return new(
                    layout.MarketWidthMeters.Value / layout.Width,
                    layout.MarketLengthMeters.Value / layout.Height,
                    "MarketLayoutPhysical");

            if (market?.BoundaryWidthMeters is > 0 && market.BoundaryHeightMeters is > 0)
                return new(
                    market.BoundaryWidthMeters.Value / (double)layout.Width,
                    market.BoundaryHeightMeters.Value / (double)layout.Height,
                    "NightMarketBoundary");
        }

        if (layout.PixelsPerMeter is > 0)
        {
            var scale = 1d / layout.PixelsPerMeter.Value;
            return new(scale, scale, "PixelsPerMeter");
        }

        if (layout.MetersPerLayoutUnit is > 0)
        {
            var scale = (double)layout.MetersPerLayoutUnit.Value;
            return new(scale, scale, "MetersPerLayoutUnit");
        }

        return null;
    }
}

public static class LayoutDistance
{
    public static decimal Between(LayoutNode from, LayoutNode to, double? pixelsPerMeter)
        => Between((double)from.Xcoordinate, (double)from.Ycoordinate, (double)to.Xcoordinate, (double)to.Ycoordinate, pixelsPerMeter);

    public static decimal Between(LayoutNode from, LayoutNode to, LayoutPhysicalScale? scale)
        => Between((double)from.Xcoordinate, (double)from.Ycoordinate, (double)to.Xcoordinate, (double)to.Ycoordinate, scale);

    public static decimal Between(double x1, double y1, double x2, double y2, double? pixelsPerMeter)
        => Between(x1, y1, x2, y2, pixelsPerMeter is > 0
            ? new LayoutPhysicalScale(1d / pixelsPerMeter.Value, 1d / pixelsPerMeter.Value, "PixelsPerMeter")
            : null);

    public static decimal Between(double x1, double y1, double x2, double y2, LayoutPhysicalScale? scale)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        if (scale is { } physical)
        {
            var meters = Math.Sqrt(
                dx * dx * physical.ScaleX * physical.ScaleX
                + dy * dy * physical.ScaleY * physical.ScaleY);
            return (decimal)Math.Max(meters, 0.01);
        }

        var units = Math.Sqrt(dx * dx + dy * dy);
        return (decimal)Math.Max(units, 0.01);
    }

    public static decimal PathMeters(
        IReadOnlyList<LayoutNode> orderedNodes,
        LayoutPhysicalScale scale)
    {
        decimal total = 0;
        for (var index = 1; index < orderedNodes.Count; index++)
            total += Between(orderedNodes[index - 1], orderedNodes[index], scale);
        return total;
    }

    public static double ClearancePixels(double? pixelsPerMeter, double clearanceMeters)
        => (pixelsPerMeter is > 0 ? pixelsPerMeter.Value : 10d) * clearanceMeters;

    public static double MinimumCorridorPixels(double? pixelsPerMeter, double minimumCorridorMeters)
        => (pixelsPerMeter is > 0 ? pixelsPerMeter.Value : 10d) * minimumCorridorMeters;
}
