using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Booths;

public sealed class BoothOperatingHoursResult
{
    public bool IsOpen { get; init; }
    public TimeOnly? NextOpenAt { get; init; }
    public string Reason { get; init; } = string.Empty;
    public BoothOperatingStatus Status { get; init; }
}

public enum BoothOperatingStatus
{
    Open,
    Closed,
    Unavailable,
    NoHours
}

public sealed class BoothOperatingHoursEvaluator
{
    public BoothOperatingHoursResult Evaluate(Booth booth, DateTime utcNow)
    {
        if (booth.Status != BoothStatus.Active)
            return new BoothOperatingHoursResult
            {
                IsOpen = false,
                Status = BoothOperatingStatus.Unavailable,
                Reason = booth.Status == BoothStatus.Banned ? "Banned" : "Paused"
            };

        return Evaluate(booth.OpenTime, booth.CloseTime, utcNow);
    }

    public BoothOperatingHoursResult Evaluate(TimeOnly? openTime, TimeOnly? closeTime, DateTime utcNow)
    {
        if (!openTime.HasValue || !closeTime.HasValue)
            return new BoothOperatingHoursResult
            {
                IsOpen = false,
                Status = BoothOperatingStatus.NoHours,
                Reason = "Operating hours not set"
            };

        var open = openTime.Value;
        var close = closeTime.Value;

        if (open == close)
            return new BoothOperatingHoursResult
            {
                IsOpen = false,
                Status = BoothOperatingStatus.NoHours,
                Reason = "Open and close times cannot be equal"
            };

        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        var isOpen = IsWithinInterval(open, close, localTime);

        return new BoothOperatingHoursResult
        {
            IsOpen = isOpen,
            Status = isOpen ? BoothOperatingStatus.Open : BoothOperatingStatus.Closed,
            NextOpenAt = isOpen ? null : ComputeNextOpenAt(open, close, localTime),
            Reason = isOpen ? "Open" : "Closed"
        };
    }

    public static bool IsWithinInterval(TimeOnly open, TimeOnly close, TimeOnly localTime)
    {
        if (open == close) return false;
        return open < close
            ? open <= localTime && localTime < close
            : localTime >= open || localTime < close;
    }

    public static TimeOnly ComputeNextOpenAt(TimeOnly open, TimeOnly close, TimeOnly localTime)
    {
        if (open < close)
        {
            return localTime < open ? open : open.AddHours(24);
        }
        else
        {
            return localTime >= open ? open.AddHours(24) : open;
        }
    }

    public static string FormatNextOpenAt(TimeOnly? nextOpenAt)
        => nextOpenAt.HasValue ? nextOpenAt.Value.ToString("HH:mm") : "soon";
}
