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
/// Converts physical dimensions into deterministic canvas geometry.
/// It mutates only derived pixel/capacity fields on the request so the legacy
/// generator and package-limit checks continue to share one source of truth.
/// </summary>
public static class PhysicalGridCalculator
{
    private const double ZoneInnerPaddingMeters = 1;
    private const double JunctionReserveMeters = 7;
    private const double ExitReserveMeters = 5;

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

        if (request.RequestedBoothCount is <= 0)
            errors.Add("Number of booth slots must be greater than zero.");

        var ppm = request.PixelsPerMeter;
        if (ppm is < 2 or > 50)
            errors.Add("Pixels per meter must be between 2 and 50.");

        request.StartX = request.StartXMeters * ppm;
        request.StartY = request.StartYMeters * ppm;
        request.ZoneMargin = request.ZoneMarginMeters * ppm;
        request.AutoExpandCanvas = false;

        if (request.AutoFitZones)
            AutoFitZones(request, errors);

        if (errors.Count > 0)
            return new PhysicalLayoutPreparation(true, errors, warnings);

        if (request.ZoneConfigs.Count == 0)
        {
            var defaultConfig = new ZoneGenerationConfig
            {
                ZoneWidthMeters = request.DefaultZoneWidthMeters,
                ZoneLengthMeters = request.DefaultZoneLengthMeters,
                BoothWidthMeters = request.DefaultBoothWidthMeters,
                BoothLengthMeters = request.DefaultBoothLengthMeters,
                HorizontalGapMeters = request.DefaultHorizontalGapMeters,
                VerticalGapMeters = request.DefaultVerticalGapMeters
            };
            var grid = Calculate(defaultConfig, ppm, "General Area", errors);
            if (grid is not null)
            {
                request.DefaultZoneCapacity = ResolveRequestedCapacities(
                    request.RequestedBoothCount,
                    [grid.Capacity],
                    errors,
                    warnings)[0];
                request.DefaultBoothWidth = grid.BoothWidthPixels;
                request.DefaultBoothHeight = grid.BoothHeightPixels;
                request.DefaultGap = grid.HorizontalGapPixels;
                request.DefaultColumns = grid.Columns;
            }
            var defaultZoneArea = (request.DefaultZoneWidthMeters ?? 0) * (request.DefaultZoneLengthMeters ?? 0);
            var marketArea = request.MarketWidthMeters.Value * request.MarketLengthMeters.Value;
            if (defaultZoneArea > marketArea + 0.0001)
                errors.Add($"General Area ({defaultZoneArea:0.##} m²) exceeds market area ({marketArea:0.##} m²).");
        }
        else
        {
            var calculatedConfigs = new List<(ZoneGenerationConfig Config, PhysicalZoneGrid Grid)>();
            foreach (var config in request.ZoneConfigs)
            {
                var grid = Calculate(config, ppm, config.ZoneName ?? "Zone", errors);
                if (grid is null)
                    continue;

                calculatedConfigs.Add((config, grid));
                config.Columns = grid.Columns;
                config.BoothWidth = grid.BoothWidthPixels;
                config.BoothHeight = grid.BoothHeightPixels;
                config.Gap = grid.HorizontalGapPixels;
            }

            if (calculatedConfigs.Count == request.ZoneConfigs.Count)
            {
                var capacities = ResolveRequestedCapacities(
                    request.RequestedBoothCount,
                    calculatedConfigs.Select(item => item.Grid.Capacity).ToList(),
                    errors,
                    warnings);
                for (var index = 0; index < calculatedConfigs.Count; index++)
                    calculatedConfigs[index].Config.Capacity = capacities[index];
            }

            var totalZoneArea = request.ZoneConfigs.Sum(config =>
                (config.ZoneWidthMeters ?? 0) * (config.ZoneLengthMeters ?? 0));
            var marketArea = request.MarketWidthMeters.Value * request.MarketLengthMeters.Value;
            if (totalZoneArea > marketArea + 0.0001)
                errors.Add($"Total zone area ({totalZoneArea:0.##} m²) exceeds market area ({marketArea:0.##} m²).");
        }

        return new PhysicalLayoutPreparation(true, errors, warnings);
    }

    private static IReadOnlyList<int> ResolveRequestedCapacities(
        int? requestedBoothCount,
        IReadOnlyList<int> physicalCapacities,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        if (!requestedBoothCount.HasValue)
            return physicalCapacities;

        if (physicalCapacities.Count == 0 || physicalCapacities.Any(capacity => capacity < 1))
            return physicalCapacities;

        if (requestedBoothCount.Value < physicalCapacities.Count)
        {
            errors.Add($"{physicalCapacities.Count} zone(s) require at least {physicalCapacities.Count} booth slots. Reduce the number of zones or increase the booth count.");
            return physicalCapacities;
        }

        var physicalTotal = physicalCapacities.Sum();
        var finalCount = Math.Min(requestedBoothCount.Value, physicalTotal);
        if (requestedBoothCount.Value > physicalTotal)
        {
            warnings.Add(
                $"The market dimensions can fit {physicalTotal} booth slots with the selected spacing, so the requested {requestedBoothCount.Value} slots were adjusted to {physicalTotal}.");
        }

        var assigned = new int[physicalCapacities.Count];
        var remaining = finalCount;

        // Each generated zone remains meaningful: reserve one booth per zone first.
        for (var index = 0; index < assigned.Length; index++)
        {
            assigned[index] = 1;
            remaining -= 1;
        }

        // Fill zones round-robin so the generated layout remains balanced.
        while (remaining > 0)
        {
            var progressed = false;
            for (var index = 0; index < assigned.Length && remaining > 0; index++)
            {
                if (assigned[index] >= physicalCapacities[index])
                    continue;
                assigned[index] += 1;
                remaining -= 1;
                progressed = true;
            }

            if (!progressed)
                break;
        }

        return assigned;
    }

    private static void AutoFitZones(GenerateLayoutRequest request, ICollection<string> errors)
    {
        var zoneConfigs = request.ZoneConfigs.Count > 0
            ? request.ZoneConfigs
            :
            [
                new ZoneGenerationConfig
                {
                    ZoneName = "General Area",
                    BoothWidthMeters = request.DefaultBoothWidthMeters ?? 3,
                    BoothLengthMeters = request.DefaultBoothLengthMeters ?? 3,
                    HorizontalGapMeters = request.DefaultHorizontalGapMeters ?? 1,
                    VerticalGapMeters = request.DefaultVerticalGapMeters ?? 1
                }
            ];

        var zoneCount = zoneConfigs.Count;
        AutoFitCandidate? best = null;
        for (var zonesPerRow = 1; zonesPerRow <= zoneCount; zonesPerRow++)
        {
            var rowCount = (int)Math.Ceiling((double)zoneCount / zonesPerRow);
            var zoneWidth = (request.MarketWidthMeters!.Value - request.StartXMeters
                - (zonesPerRow - 1) * request.ZoneMarginMeters) / zonesPerRow;
            var zoneLength = (request.MarketLengthMeters!.Value - request.StartYMeters - ExitReserveMeters
                - rowCount * JunctionReserveMeters - (rowCount - 1) * request.ZoneMarginMeters) / rowCount;
            if (zoneWidth <= 0 || zoneLength <= 0)
                continue;

            var totalCapacity = 0;
            var fits = true;
            foreach (var _ in zoneConfigs)
            {
                const double boothWidth = 3;
                const double boothLength = 3;
                const double gapX = 1;
                const double gapY = 1;
                var columns = Math.Min(4, (int)Math.Floor((zoneWidth - ZoneInnerPaddingMeters * 2 + gapX) / (boothWidth + gapX)));
                var rows = (int)Math.Floor((zoneLength - ZoneInnerPaddingMeters * 2 + gapY) / (boothLength + gapY));
                if (columns < 1 || rows < 1)
                {
                    fits = false;
                    break;
                }
                totalCapacity = checked(totalCapacity + columns * rows);
            }

            if (fits && (best is null || totalCapacity > best.TotalCapacity))
                best = new AutoFitCandidate(zonesPerRow, zoneWidth, zoneLength, totalCapacity);
        }

        if (best is null)
        {
            errors.Add(
                $"The market is too small for {zoneCount} zone(s). Increase the market size or reduce the number of zones so every zone has room for at least one booth and a safe walkway.");
            return;
        }

        request.ZonesPerRow = best.ZonesPerRow;
        if (request.ZoneConfigs.Count == 0)
        {
            request.DefaultZoneWidthMeters = best.ZoneWidth;
            request.DefaultZoneLengthMeters = best.ZoneLength;
            request.DefaultBoothWidthMeters = 3;
            request.DefaultBoothLengthMeters = 3;
            request.DefaultHorizontalGapMeters = 1;
            request.DefaultVerticalGapMeters = 1;
            return;
        }

        foreach (var config in request.ZoneConfigs)
        {
            config.ZoneWidthMeters = best.ZoneWidth;
            config.ZoneLengthMeters = best.ZoneLength;
            config.BoothWidthMeters = 3;
            config.BoothLengthMeters = 3;
            config.HorizontalGapMeters = 1;
            config.VerticalGapMeters = 1;
        }
    }

    private sealed record AutoFitCandidate(int ZonesPerRow, double ZoneWidth, double ZoneLength, int TotalCapacity);

    public static PhysicalZoneGrid? Calculate(
        ZoneGenerationConfig config,
        double pixelsPerMeter,
        string zoneLabel,
        ICollection<string> errors)
    {
        if (config.ZoneWidthMeters is not > 0 || config.ZoneLengthMeters is not > 0
            || config.BoothWidthMeters is not > 0 || config.BoothLengthMeters is not > 0)
        {
            errors.Add($"Zone '{zoneLabel}' requires zone width/length and booth width/length in meters.");
            return null;
        }

        var gapX = config.HorizontalGapMeters ?? 0;
        var gapY = config.VerticalGapMeters ?? 0;
        if (gapX < 0 || gapY < 0)
        {
            errors.Add($"Zone '{zoneLabel}' booth gaps cannot be negative.");
            return null;
        }

        var usableWidth = config.ZoneWidthMeters.Value - ZoneInnerPaddingMeters * 2;
        var usableLength = config.ZoneLengthMeters.Value - ZoneInnerPaddingMeters * 2;
        // A fixed maximum of four columns keeps the visual grid readable and
        // matches the renderer. Additional capacity is added as rows instead
        // of producing a long, hard-to-use horizontal strip.
        var columns = Math.Min(4, (int)Math.Floor((usableWidth + gapX) / (config.BoothWidthMeters.Value + gapX)));
        var rows = (int)Math.Floor((usableLength + gapY) / (config.BoothLengthMeters.Value + gapY));
        if (columns < 1 || rows < 1)
        {
            errors.Add($"Zone '{zoneLabel}' is too small to contain one booth with the selected dimensions.");
            return null;
        }

        return new PhysicalZoneGrid(
            columns,
            rows,
            checked(columns * rows),
            config.ZoneWidthMeters.Value * pixelsPerMeter,
            config.ZoneLengthMeters.Value * pixelsPerMeter,
            config.BoothWidthMeters.Value * pixelsPerMeter,
            config.BoothLengthMeters.Value * pixelsPerMeter,
            gapX * pixelsPerMeter,
            gapY * pixelsPerMeter);
    }
}
