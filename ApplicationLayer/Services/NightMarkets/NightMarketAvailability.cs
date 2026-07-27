using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.NightMarkets;

public static class NightMarketAvailability
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static DateTime GetVietnamLocalTime(DateTime utcNow)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            VietnamTimeZone);

    public static NightMarketAvailabilityResult Evaluate(
        NightMarketCustomerReadModel market,
        DateTime utcNow)
    {
        if (market.Status != NightMarketStatus.Active)
            return new NightMarketAvailabilityResult(false, "Inactive");

        if (
            !market.OpeningHours.HasValue ||
            !market.ClosingHours.HasValue)
            return new NightMarketAvailabilityResult(false, "Hours unavailable");

        var localTime = TimeOnly.FromDateTime(GetVietnamLocalTime(utcNow));
        var isOpen = IsWithinSchedule(
            market.OpeningHours.Value,
            market.ClosingHours.Value,
            localTime);

        return isOpen
            ? new NightMarketAvailabilityResult(
                true,
                $"Open now - Closes at {market.ClosingHours.Value:HH:mm}")
            : new NightMarketAvailabilityResult(
                false,
                $"Closed - Opens at {market.OpeningHours.Value:HH:mm}");
    }

    public static bool IsWithinSchedule(TimeOnly opening, TimeOnly closing, TimeOnly localTime)
    {
        if (opening == closing)
            return false;

        return opening < closing
            ? opening <= localTime && localTime < closing
            : localTime >= opening || localTime < closing;
    }
}

public sealed record NightMarketAvailabilityResult(bool IsOpenNow, string StatusText);
