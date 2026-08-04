using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs;

public class PackageTemplateResponse
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PackageType PackageType { get; set; }
    public bool IsFree { get; set; }
    public List<string> Features { get; set; } = new();
}

public static class PackageTemplateHelper
{
    public static readonly IReadOnlyList<PackageTemplateResponse> Templates = new List<PackageTemplateResponse>
    {
        new()
        {
            Code = "MARKET_BASIC",
            DisplayName = "Market Basic",
            PackageType = PackageType.Market,
            IsFree = false,
            Features = new List<string>
            {
                "Manage 1 night market",
                "Up to 30 booth slots per market",
                "1 layout per market",
                "Manage market information and operating hours",
                "Create and manage booth slots",
                "Assign booths to available slots",
                "Basic complaint management",
                "View basic booth and order statistics"
            }
        },
        new()
        {
            Code = "MARKET_PRO",
            DisplayName = "Market Pro",
            PackageType = PackageType.Market,
            IsFree = false,
            Features = new List<string>
            {
                "Includes all Market Basic features",
                "Manage up to 3 night markets",
                "Up to 150 booth slots per market",
                "Up to 5 layouts per market",
                "Create Food, Drink and Dessert zones",
                "Advanced booth management",
                "Booth risk indicators",
                "Priority complaint handling and SLA tracking",
                "Advanced complaint dashboard",
                "Market activity reports",
                "Booth performance reports",
                "Sales activity insights",
                "Export reports"
            }
        },
        new()
        {
            Code = "BOOTH_FREE",
            DisplayName = "Booth Basic",
            PackageType = PackageType.Booth,
            IsFree = true,
            Features = new List<string>
            {
                "Manage one booth created and assigned by a Market Owner",
                "Manage up to 20 menu items",
                "Receive and process orders",
                "Create orders for customers",
                "Chat with customers",
                "View customer reviews",
                "View basic order, revenue and rating statistics",
                "No expiration and no payment required"
            }
        },
        new()
        {
            Code = "BOOTH_GROWTH",
            DisplayName = "Booth Boost",
            PackageType = PackageType.Booth,
            IsFree = false,
            Features = new List<string>
            {
                "Includes all Booth Basic features",
                "Unlimited menu items",
                "Reply to customer reviews",
                "View best-selling item analytics",
                "View peak-hour analytics",
                "Create promotions and combos",
                "Tier 2 recommendation priority"
            }
        },
        new()
        {
            Code = "BOOTH_FEATURED",
            DisplayName = "Booth Featured",
            PackageType = PackageType.Booth,
            IsFree = false,
            Features = new List<string>
            {
                "Includes all Booth Boost features",
                "Featured booth badge",
                "Higher visibility in relevant booth discovery",
                "Highest recommendation priority among Booth plans",
                "Feature selected food items"
            }
        }
    };

    public static (string EntitlementsJson, PackageType PackageType, bool IsFree) ResolveTemplate(string? templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            throw new Exceptions.AppException("Template code is required.", 400, "TEMPLATE_CODE_REQUIRED");

        return templateCode.ToUpperInvariant() switch
        {
            "MARKET_BASIC" => (EntitlementHelper.SerializeMarket(MarketEntitlements.Basic), PackageType.Market, false),
            "MARKET_PRO" => (EntitlementHelper.SerializeMarket(MarketEntitlements.Pro), PackageType.Market, false),
            "BOOTH_FREE" => (EntitlementHelper.SerializeBooth(BoothEntitlements.Free), PackageType.Booth, true),
            "BOOTH_GROWTH" => (EntitlementHelper.SerializeBooth(BoothEntitlements.Growth), PackageType.Booth, false),
            "BOOTH_FEATURED" => (EntitlementHelper.SerializeBooth(BoothEntitlements.Featured), PackageType.Booth, false),
            _ => throw new Exceptions.AppException("Invalid package template code.", 400, "INVALID_PACKAGE_TEMPLATE")
        };
    }

    public static bool IsValidTemplate(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var upper = code.ToUpperInvariant();
        return upper is "MARKET_BASIC" or "MARKET_PRO" or "BOOTH_FREE" or "BOOTH_GROWTH" or "BOOTH_FEATURED";
    }

    public static List<string> GetFeatures(string? code)
    {
        return Templates
            .FirstOrDefault(t =>
                string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase))
            ?.Features
            .ToList() ?? new List<string>();
    }
}
