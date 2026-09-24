using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.MarketLayouts;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.MarketMaps;

/// <summary>
/// Shared physical-coordinate calculation for management preview and the
/// published customer aggregate.
/// </summary>
public static class MarketMapGeometry
{
    public static MarketMapOverallBoundsResponse? CalculateOverallBounds(
        IReadOnlyCollection<MarketLayout> layouts,
        NightMarket market)
        => CalculateOverallBounds(
            layouts, market.BoundaryWidthMeters, market.BoundaryHeightMeters);

    public static MarketMapOverallBoundsResponse? CalculateOverallBounds(
        IReadOnlyCollection<MarketLayout> layouts,
        double? boundaryWidthMeters,
        double? boundaryHeightMeters)
    {
        if (layouts.Count == 0)
            return null;

        var rectangles = new List<PhysicalLayoutRect>(layouts.Count);
        foreach (var layout in layouts)
        {
            var scale = LayoutPhysicalCalibration.TryResolve(
                layout, boundaryWidthMeters, boundaryHeightMeters);
            if (!scale.HasValue || layout.Width <= 0 || layout.Height <= 0)
                return null;

            var width = layout.Width * scale.Value.ScaleX;
            var height = layout.Height * scale.Value.ScaleY;
            if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                return null;

            rectangles.Add(new(
                layout.OffsetXMeters,
                layout.OffsetYMeters,
                width,
                height));
        }

        var minX = rectangles.Min(rectangle => rectangle.X);
        var minY = rectangles.Min(rectangle => rectangle.Y);
        var maxX = rectangles.Max(rectangle => rectangle.Right);
        var maxY = rectangles.Max(rectangle => rectangle.Bottom);
        return new MarketMapOverallBoundsResponse
        {
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
            OverallWidthMeters = maxX - minX,
            OverallHeightMeters = maxY - minY
        };
    }

    private readonly record struct PhysicalLayoutRect(
        double X, double Y, double Width, double Height)
    {
        public double Right => X + Width;
        public double Bottom => Y + Height;
    }
}
