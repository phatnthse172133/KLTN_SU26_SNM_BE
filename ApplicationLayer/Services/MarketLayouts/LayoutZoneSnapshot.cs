using System.Text.Json;
using System.Text.Json.Nodes;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.MarketLayouts;

// Zone is a market-level identity. Its layout-specific configuration belongs
// to the block so that editing a draft cannot change a published layout.
public static class LayoutZoneSnapshot
{
    public static Zone Copy(Zone z) => new()
    {
        Id = z.Id, NightMarketId = z.NightMarketId, ZoneName = z.ZoneName,
        Description = z.Description, Color = z.Color, ZoneCode = z.ZoneCode,
        Capacity = z.Capacity, DefaultBoothWidth = z.DefaultBoothWidth,
        DefaultBoothHeight = z.DefaultBoothHeight, DefaultGap = z.DefaultGap,
        WidthMeters = z.WidthMeters, LengthMeters = z.LengthMeters,
        BoothWidthMeters = z.BoothWidthMeters, BoothLengthMeters = z.BoothLengthMeters,
        HorizontalGapMeters = z.HorizontalGapMeters, VerticalGapMeters = z.VerticalGapMeters,
        Status = z.Status, IsDeleted = z.IsDeleted, CreatedAt = z.CreatedAt, UpdatedAt = z.UpdatedAt
    };

    public static IReadOnlyCollection<Zone> Resolve(IReadOnlyCollection<Zone> zones, IReadOnlyCollection<LayoutBlock> blocks)
        => zones.Select(zone =>
        {
            var copy = Copy(zone);
            var block = blocks.FirstOrDefault(b => !b.IsDeleted && b.ZoneId == zone.Id);
            if (block is null) return copy;
            if (!string.IsNullOrWhiteSpace(block.Name)) copy.ZoneName = block.Name;
            try
            {
                using var doc = JsonDocument.Parse(block.ConfigJson ?? "{}");
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("zoneSnapshot", out var snapshot)) return copy;
                var saved = JsonSerializer.Deserialize<Zone>(snapshot.GetRawText());
                if (saved is not null && saved.Id == zone.Id) return Copy(saved);
            }
            catch (JsonException) { }
            return copy;
        }).ToList();

    public static string Store(string? configJson, Zone zone)
    {
        JsonObject config;
        try { config = JsonNode.Parse(configJson ?? "{}") as JsonObject ?? new JsonObject(); }
        catch (JsonException) { config = new JsonObject(); }
        config["zoneSnapshot"] = JsonSerializer.SerializeToNode(Copy(zone));
        return config.ToJsonString();
    }
}
