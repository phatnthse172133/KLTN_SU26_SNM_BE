using ApplicationLayer.DTOs.Requests;

namespace ApplicationLayer.Services.MarketLayouts;

public sealed record PhysicalZoneGrid(
    int Columns,
    int Rows,
    int Capacity,
    double ZoneWidthPixels,
    double ZoneHeightPixels,
    double BoothWidthPixels,
    double BoothHeightPixels,
    double HorizontalGapPixels,
    double VerticalGapPixels);

public sealed record PhysicalLayoutPreparation(
    bool IsPhysical,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Converts physical dimensions into deterministic canvas geometry without modifying
/// user-specified zone sizes, booth sizes, or requested capacities.
/// It strictly computes maximum capacity, validates boundary and area constraints,
/// and returns descriptive errors and warnings.
/// </summary>
public static class PhysicalGridCalculator
{
    public const double ZoneInnerPaddingMeters = 1.0;

    public static PhysicalLayoutPreparation Prepare(GenerateLayoutRequest request)
    {
        var physical = request.MarketWidthMeters.HasValue || request.MarketLengthMeters.HasValue;
        if (!physical)
            return new PhysicalLayoutPreparation(false, [], []);

        var errors = new List<string>();
        var warnings = new List<string>();

        if (request.MarketWidthMeters is not > 0 || request.MarketLengthMeters is not > 0)
        {
            errors.Add("Market width and length must both be greater than zero.");
            return new PhysicalLayoutPreparation(true, errors, warnings);
        }

        var ppm = request.PixelsPerMeter;
        if (ppm is < 2 or > 50)
            errors.Add("Pixels per meter must be between 2 and 50.");

        request.StartX = request.StartXMeters * ppm;
        request.StartY = request.StartYMeters * ppm;
        request.ZoneMargin = request.ZoneMarginMeters * ppm;
        request.AutoExpandCanvas = false;
        request.AutoFitZones = false;

        var marketArea = request.MarketWidthMeters.Value * request.MarketLengthMeters.Value;

        if (request.ZoneConfigs.Count == 0)
        {
            var defaultConfig = new ZoneGenerationConfig
            {
                ZoneWidthMeters = request.DefaultZoneWidthMeters,
                ZoneLengthMeters = request.DefaultZoneLengthMeters,
                BoothWidthMeters = request.DefaultBoothWidthMeters,
                BoothLengthMeters = request.DefaultBoothLengthMeters,
                HorizontalGapMeters = request.DefaultHorizontalGapMeters,
                VerticalGapMeters = request.DefaultVerticalGapMeters,
                Capacity = request.DefaultZoneCapacity ?? request.RequestedBoothCount
            };
            var grid = Calculate(defaultConfig, ppm, "General Area", errors);
            if (grid is not null)
            {
                request.DefaultBoothWidth = grid.BoothWidthPixels;
                request.DefaultBoothHeight = grid.BoothHeightPixels;
                request.DefaultGap = grid.HorizontalGapPixels;
                request.DefaultColumns = grid.Columns;

                var requestedCapacity = request.DefaultZoneCapacity ?? request.RequestedBoothCount ?? 0;
                if (requestedCapacity > grid.Capacity)
                {
                    errors.Add($"Zone 'General Area' chỉ chứa tối đa {grid.Capacity} booth nhưng đang yêu cầu {requestedCapacity} booth.");
                }
                else if (requestedCapacity <= 0)
                {
                    errors.Add("Zone 'General Area' yêu cầu số lượng booth lớn hơn 0.");
                }
            }

            var defaultZoneArea = (request.DefaultZoneWidthMeters ?? 0) * (request.DefaultZoneLengthMeters ?? 0);
            if (defaultZoneArea > marketArea + 0.0001)
                errors.Add($"Diện tích General Area ({defaultZoneArea:0.##} m²) vượt diện tích khu chợ ({marketArea:0.##} m²) là {(defaultZoneArea - marketArea):0.##} m².");
        }
        else
        {
            foreach (var config in request.ZoneConfigs)
            {
                var grid = Calculate(config, ppm, config.ZoneName ?? "Zone", errors);
                if (grid is null)
                    continue;

                config.Columns = grid.Columns;
                config.BoothWidth = grid.BoothWidthPixels;
                config.BoothHeight = grid.BoothHeightPixels;
                config.Gap = grid.HorizontalGapPixels;

                var requestedCapacity = config.Capacity ?? 0;
                if (requestedCapacity > grid.Capacity)
                {
                    errors.Add($"Zone '{config.ZoneName ?? "Zone"}' chỉ chứa tối đa {grid.Capacity} booth nhưng đang yêu cầu {requestedCapacity} booth.");
                }
                else if (requestedCapacity <= 0)
                {
                    errors.Add($"Zone '{config.ZoneName ?? "Zone"}' yêu cầu số lượng booth lớn hơn 0.");
                }
            }

            var totalZoneArea = request.ZoneConfigs.Sum(config =>
                (config.ZoneWidthMeters ?? 0) * (config.ZoneLengthMeters ?? 0));

            if (totalZoneArea > marketArea + 0.0001)
                errors.Add($"Tổng diện tích các zone ({totalZoneArea:0.##} m²) vượt diện tích khu chợ ({marketArea:0.##} m²) là {(totalZoneArea - marketArea):0.##} m².");
        }

        return new PhysicalLayoutPreparation(true, errors, warnings);
    }

    public static PhysicalZoneGrid? Calculate(
        ZoneGenerationConfig config,
        double pixelsPerMeter,
        string zoneLabel,
        ICollection<string> errors)
    {
        if (config.ZoneWidthMeters is not > 0 || config.ZoneLengthMeters is not > 0
            || config.BoothWidthMeters is not > 0 || config.BoothLengthMeters is not > 0)
        {
            errors.Add($"Zone '{zoneLabel}' yêu cầu chiều rộng, chiều dài zone và kích thước booth hợp lệ (> 0m).");
            return null;
        }

        var gapX = config.HorizontalGapMeters ?? 0;
        var gapY = config.VerticalGapMeters ?? 0;
        if (gapX < 0 || gapY < 0)
        {
            errors.Add($"Zone '{zoneLabel}' khoảng cách giữa các booth không được âm.");
            return null;
        }

        var usableWidth = config.ZoneWidthMeters.Value - ZoneInnerPaddingMeters * 2;
        var usableLength = config.ZoneLengthMeters.Value - ZoneInnerPaddingMeters * 2;

        var columns = (int)Math.Floor((usableWidth + gapX) / (config.BoothWidthMeters.Value + gapX));
        var rows = (int)Math.Floor((usableLength + gapY) / (config.BoothLengthMeters.Value + gapY));

        if (columns < 1 || rows < 1)
        {
            errors.Add($"Zone '{zoneLabel}' có kích thước {config.ZoneWidthMeters:0.##}m x {config.ZoneLengthMeters:0.##}m quá nhỏ để chứa 1 booth {config.BoothWidthMeters:0.##}m x {config.BoothLengthMeters:0.##}m với khoảng cách đã chọn.");
            return null;
        }

        var maxCapacity = checked(columns * rows);

        return new PhysicalZoneGrid(
            columns,
            rows,
            maxCapacity,
            config.ZoneWidthMeters.Value * pixelsPerMeter,
            config.ZoneLengthMeters.Value * pixelsPerMeter,
            config.BoothWidthMeters.Value * pixelsPerMeter,
            config.BoothLengthMeters.Value * pixelsPerMeter,
            gapX * pixelsPerMeter,
            gapY * pixelsPerMeter);
    }
}
