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
                "1 market",
                "30 slots per market",
                "1 layout per market",
                "Operating hours management",
                "Approve booth registrations",
                "Assign booths to slots",
                "Basic complaint management (no SLA)",
                "Statistics on number of booths and registrations"
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
                "Inherits all features from Market Basic",
                "3 markets",
                "150 slots per market",
                "Manage multiple layouts (up to 5 layouts per market)",
                "Zone management",
                "Advanced booth approval filters",
                "Booth owner risk indicators",
                "Advanced complaint management (priority + SLA)",
                "Advanced complaint dashboard",
                "Per-market activity reports",
                "Booth performance reports",
                "Sales activity insights",
                "Export reports as PDF/Excel"
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
                "Menu limited to 20 items",
                "Receive orders",
                "Create orders for customers",
                "Chat with customers",
                "View reviews but cannot reply",
                "Basic statistics: order count, revenue and rating",
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
                "Reply to reviews",
                "Analytics for best-selling items and peak hours",
                "Create promotions",
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
                "Featured badge",
                "Priority placement in recommendations",
                "Featured food",
                "Create promotions",
                "Tier 3 recommendation priority"
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
