using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.CustomerDiscovery;

public static class CustomerOrderability
{
    public const string MarketUnavailable = "MARKET_UNAVAILABLE";
    public const string MarketClosed = "MARKET_CLOSED";
    public const string BoothUnavailable = "BOOTH_UNAVAILABLE";
    public const string BoothClosed = "BOOTH_CLOSED";
    public const string FoodUnavailable = "FOOD_NOT_AVAILABLE";

    public static CustomerOrderabilityResult Evaluate(FoodItem foodItem, DateTime utcNow)
    {
        var booth = foodItem.Booth;
        var market = booth?.NightMarket;

        if (market is null
            || market.IsDeleted
            || market.ModerationStatus != ModerationStatus.Active)
        {
            return Blocked(MarketUnavailable);
        }

        if (market.Status != NightMarketStatus.Active)
            return Blocked(MarketClosed);

        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        if (!CustomerAvailability.IsWithinInterval(
                market.OpeningHours,
                market.ClosingHours,
                localTime))
        {
            return Blocked(MarketClosed);
        }

        if (booth!.Status != BoothStatus.Active)
            return Blocked(BoothUnavailable);

        if (booth.OpenTime.HasValue != booth.CloseTime.HasValue
            || (booth.OpenTime.HasValue
                && !CustomerAvailability.IsWithinInterval(
                    booth.OpenTime,
                    booth.CloseTime,
                    localTime)))
        {
            return Blocked(BoothClosed);
        }

        if (foodItem.IsDeleted
            || !foodItem.IsAvailable
            || foodItem.Category is null
            || foodItem.Category.IsDeleted)
        {
            return Blocked(FoodUnavailable);
        }

        return new CustomerOrderabilityResult(true, null);
    }

    public static string GetPublicMessage(string reasonCode)
        => reasonCode switch
        {
            MarketUnavailable => "The night market is not available for ordering.",
            MarketClosed => "The night market is currently closed.",
            BoothUnavailable => "The booth is not available for ordering.",
            BoothClosed => "The booth is currently closed.",
            _ => "The food item is currently unavailable."
        };

    private static CustomerOrderabilityResult Blocked(string reasonCode)
        => new(false, reasonCode);
}

public sealed record CustomerOrderabilityResult(bool CanOrder, string? ReasonCode);
