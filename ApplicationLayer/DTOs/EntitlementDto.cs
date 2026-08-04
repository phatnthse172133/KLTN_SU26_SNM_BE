using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplicationLayer.DTOs;

public class MarketEntitlements
{
    [JsonPropertyName("maxMarkets")]
    public int MaxMarkets { get; set; } = 1;

    [JsonPropertyName("maxSlotsPerMarket")]
    public int MaxSlotsPerMarket { get; set; } = 30;

    [JsonPropertyName("maxLayoutsPerMarket")]
    public int? MaxLayoutsPerMarket { get; set; }

    [JsonPropertyName("zoneManagement")]
    public bool ZoneManagement { get; set; }

    [JsonPropertyName("advancedBoothApproval")]
    public bool AdvancedBoothApproval { get; set; }

    [JsonPropertyName("advancedComplaint")]
    public bool AdvancedComplaint { get; set; }

    [JsonPropertyName("advancedReports")]
    public bool AdvancedReports { get; set; }

    [JsonPropertyName("aiInsights")]
    public bool AiInsights { get; set; }

    [JsonPropertyName("exportReports")]
    public bool ExportReports { get; set; }

    public static MarketEntitlements Basic => new()
    {
        MaxMarkets = 1,
        MaxSlotsPerMarket = 30,
        MaxLayoutsPerMarket = 1,
        ZoneManagement = false,
        AdvancedBoothApproval = false,
        AdvancedComplaint = false,
        AdvancedReports = false,
        AiInsights = false,
        ExportReports = false
    };

    public static MarketEntitlements None => new()
    {
        MaxMarkets = 0,
        MaxSlotsPerMarket = 0,
        MaxLayoutsPerMarket = 0,
        ZoneManagement = false,
        AdvancedBoothApproval = false,
        AdvancedComplaint = false,
        AdvancedReports = false,
        AiInsights = false,
        ExportReports = false
    };

    public static MarketEntitlements Pro => new()
    {
        MaxMarkets = 3,
        MaxSlotsPerMarket = 150,
        MaxLayoutsPerMarket = 5,
        ZoneManagement = true,
        AdvancedBoothApproval = true,
        AdvancedComplaint = true,
        AdvancedReports = true,
        AiInsights = true,
        ExportReports = true
    };
}

public class BoothEntitlements
{
    [JsonPropertyName("maxBooths")]
    public int MaxBooths { get; set; } = 1;

    [JsonPropertyName("maxMenuItems")]
    public int? MaxMenuItems { get; set; }

    [JsonPropertyName("promotion")]
    public bool Promotion { get; set; }

    [JsonPropertyName("advancedAnalytics")]
    public bool AdvancedAnalytics { get; set; }

    [JsonPropertyName("reviewReply")]
    public bool ReviewReply { get; set; }

    [JsonPropertyName("pauseBooth")]
    public bool PauseBooth { get; set; }

    [JsonPropertyName("recommendationPriority")]
    public double RecommendationPriority { get; set; } = 1.0;

    [JsonPropertyName("featuredBooth")]
    public bool FeaturedBooth { get; set; }

    [JsonPropertyName("featuredFood")]
    public bool FeaturedFood { get; set; }

    public static BoothEntitlements Free => new()
    {
        MaxBooths = 1,
        MaxMenuItems = 20,
        Promotion = false,
        AdvancedAnalytics = false,
        ReviewReply = false,
        PauseBooth = false,
        RecommendationPriority = 1.0,
        FeaturedBooth = false,
        FeaturedFood = false
    };

    public static BoothEntitlements Growth => new()
    {
        MaxBooths = 1,
        MaxMenuItems = null,
        Promotion = true,
        AdvancedAnalytics = true,
        ReviewReply = true,
        PauseBooth = true,
        RecommendationPriority = 1.2,
        FeaturedBooth = false,
        FeaturedFood = false
    };

    public static BoothEntitlements Featured => new()
    {
        MaxBooths = 1,
        MaxMenuItems = null,
        Promotion = true,
        AdvancedAnalytics = true,
        ReviewReply = true,
        PauseBooth = true,
        RecommendationPriority = 1.5,
        FeaturedBooth = true,
        FeaturedFood = true
    };
}

public static class EntitlementHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeMarket(MarketEntitlements e) => JsonSerializer.Serialize(e, JsonOpts);
    public static string SerializeBooth(BoothEntitlements e) => JsonSerializer.Serialize(e, JsonOpts);

    public static MarketEntitlements DeserializeMarket(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return MarketEntitlements.None;
        try { return JsonSerializer.Deserialize<MarketEntitlements>(json, JsonOpts) ?? MarketEntitlements.None; }
        catch { return MarketEntitlements.None; }
    }

    public static MarketEntitlements DeserializeMarketStrict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new System.Text.Json.JsonException("Market entitlements JSON is empty.");
        return JsonSerializer.Deserialize<MarketEntitlements>(json, JsonOpts)
            ?? throw new System.Text.Json.JsonException("Market entitlements JSON deserialized to null.");
    }

    public static BoothEntitlements DeserializeBooth(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return BoothEntitlements.Free;
        try { return JsonSerializer.Deserialize<BoothEntitlements>(json, JsonOpts) ?? BoothEntitlements.Free; }
        catch { return BoothEntitlements.Free; }
    }

    public static BoothEntitlements DeserializeBoothStrict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new System.Text.Json.JsonException("Booth entitlements JSON is empty.");
        return JsonSerializer.Deserialize<BoothEntitlements>(json, JsonOpts)
            ?? throw new System.Text.Json.JsonException("Booth entitlements JSON deserialized to null.");
    }
}
