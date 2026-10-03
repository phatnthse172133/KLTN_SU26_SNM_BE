using ApplicationLayer.Exceptions;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.MarketLayouts;

/// <summary>Swap layouts are alternative designs of the same complete market.</summary>
public static class FullMarketLayoutDimensions
{
    public static void Apply(MarketLayout layout, NightMarket market)
    {
        var width = market.BoundaryWidthMeters ?? 0;
        var length = market.BoundaryHeightMeters ?? 0;
        if (!double.IsFinite(width) || !double.IsFinite(length) || width <= 0 || length <= 0)
            throw AppException.BadRequest("Set the market width and length in Market Details before creating or editing a layout.", "MARKET_BOUNDARY_MISSING");

        // Retain calibration: expanding a legacy canvas must not stretch its contents.
        var ppm = layout.PixelsPerMeter is > 0 && double.IsFinite(layout.PixelsPerMeter.Value)
            ? layout.PixelsPerMeter.Value : 10;
        var canvasWidth = Math.Ceiling(width * ppm);
        var canvasHeight = Math.Ceiling(length * ppm);
        if (canvasWidth > 50000 || canvasHeight > 50000)
            throw AppException.BadRequest("The calculated market canvas exceeds the supported size of 50,000 pixels per side.", "LAYOUT_CANVAS_TOO_LARGE");

        layout.MarketWidthMeters = width;
        layout.MarketLengthMeters = length;
        layout.OffsetXMeters = 0;
        layout.OffsetYMeters = 0;
        layout.PixelsPerMeter = ppm;
        layout.Width = (int)canvasWidth;
        layout.Height = (int)canvasHeight;
    }
}
