using ApplicationLayer.Services.Booths;
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

    /// <summary>
    /// Full evaluation for checkout/order creation.
    /// Blocks if booth is closed (outside operating hours) OR unavailable (Inactive/Banned).
    /// </summary>
    public static CustomerOrderabilityResult Evaluate(FoodItem foodItem, DateTime utcNow)
    {
        var (booth, market) = ValidateMarket(foodItem);
        if (market is null) return Blocked(MarketUnavailable);
        if (market.Status != NightMarketStatus.Active) return Blocked(MarketClosed);

        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        if (!CustomerAvailability.IsWithinInterval(
                market.OpeningHours, market.ClosingHours, localTime))
            return Blocked(MarketClosed);

        if (booth!.Status != BoothStatus.Active)
            return Blocked(BoothUnavailable);

        if (booth.OpenTime.HasValue != booth.CloseTime.HasValue
            || (booth.OpenTime.HasValue
                && !CustomerAvailability.IsWithinInterval(
                    booth.OpenTime, booth.CloseTime, localTime)))
        {
            var nextOpen = ComputeNextOpenAt(booth, localTime);
            return new CustomerOrderabilityResult(false, BoothClosed, NextOpenAt: nextOpen);
        }

        if (IsFoodUnavailable(foodItem))
            return Blocked(FoodUnavailable);

        return new CustomerOrderabilityResult(true, null);
    }

    /// <summary>
    /// Evaluation for cart add operations.
    /// Blocks inactive/unavailable market, booth, or food only.
    /// Does NOT block when market or booth is outside operating hours —
    /// customers can add items while closed and checkout later when open.
    /// </summary>
    public static CustomerOrderabilityResult EvaluateForCartAdd(FoodItem foodItem, DateTime utcNow)
    {
        _ = utcNow;
        var (booth, market) = ValidateMarket(foodItem);
        if (market is null) return Blocked(MarketUnavailable);
        if (market.Status != NightMarketStatus.Active) return Blocked(MarketClosed);

        if (booth!.Status != BoothStatus.Active)
            return Blocked(BoothUnavailable);

        if (IsFoodUnavailable(foodItem))
            return Blocked(FoodUnavailable);

        return new CustomerOrderabilityResult(true, null);
    }

    /// <summary>
    /// Cart-add / browse affordance: food is orderable into cart when entities are
    /// active and food is available — independent of current opening hours.
    /// </summary>
    public static bool CanAddToCart(FoodItem foodItem, DateTime utcNow)
        => EvaluateForCartAdd(foodItem, utcNow).CanOrder;

    public static string GetPublicMessage(string reasonCode)
        => reasonCode switch
        {
            MarketUnavailable => "The night market is not available for ordering.",
            MarketClosed => "The night market is currently closed.",
            BoothUnavailable => "The booth is not available for ordering.",
            BoothClosed => "The booth is currently closed.",
            _ => "The food item is currently unavailable."
        };

    private static (Booth? Booth, NightMarket? Market) ValidateMarket(FoodItem foodItem)
    {
        var booth = foodItem.Booth;
        var market = booth?.NightMarket;

        if (market is null
            || market.IsDeleted
            || market.ModerationStatus != ModerationStatus.Active)
        {
            return (booth, null);
        }

        return (booth, market);
    }

    private static bool IsFoodUnavailable(FoodItem foodItem)
        => foodItem.IsDeleted
           || !foodItem.IsAvailable
           || foodItem.Category is null
           || foodItem.Category.IsDeleted;

    private static TimeOnly? ComputeNextOpenAt(Booth booth, TimeOnly localTime)
    {
        if (!booth.OpenTime.HasValue || !booth.CloseTime.HasValue)
            return null;
        return BoothOperatingHoursEvaluator.ComputeNextOpenAt(
            booth.OpenTime.Value, booth.CloseTime.Value, localTime);
    }

    private static CustomerOrderabilityResult Blocked(string reasonCode)
        => new(false, reasonCode);
}

public sealed record CustomerOrderabilityResult(bool CanOrder, string? ReasonCode, TimeOnly? NextOpenAt = null);
