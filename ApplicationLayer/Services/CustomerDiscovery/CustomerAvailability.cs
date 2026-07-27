using DomainLayer.Common;

namespace ApplicationLayer.Services.CustomerDiscovery;

public static class CustomerAvailability
{
    public static bool IsOpenNow(CustomerBoothReadModel booth, TimeOnly localTime)
        => IsOpenNow(
            booth.MarketIsOperational,
            booth.MarketOpenTime,
            booth.MarketCloseTime,
            booth.OpenTime,
            booth.CloseTime,
            localTime);

    public static bool IsOpenNow(CustomerFoodReadModel food, TimeOnly localTime)
        => IsOpenNow(
            food.MarketIsOperational,
            food.MarketOpenTime,
            food.MarketCloseTime,
            food.BoothOpenTime,
            food.BoothCloseTime,
            localTime);

    public static bool IsOpenNow(NightMarketBoothCustomerReadModel booth, TimeOnly localTime)
        => IsOpenNow(
            booth.MarketIsOperational,
            booth.MarketOpenTime,
            booth.MarketCloseTime,
            booth.OpenTime,
            booth.CloseTime,
            localTime);

    public static bool IsOpenNow(NightMarketFoodCustomerReadModel food, TimeOnly localTime)
        => IsOpenNow(
            food.MarketIsOperational,
            food.MarketOpenTime,
            food.MarketCloseTime,
            food.BoothOpenTime,
            food.BoothCloseTime,
            localTime);

    public static bool IsOpenNow(
        bool marketIsOperational,
        TimeOnly? marketOpen,
        TimeOnly? marketClose,
        TimeOnly? boothOpen,
        TimeOnly? boothClose,
        TimeOnly localTime)
    {
        if (!marketIsOperational || !IsWithinInterval(marketOpen, marketClose, localTime))
            return false;
        if (boothOpen.HasValue != boothClose.HasValue) return false;
        return !boothOpen.HasValue || IsWithinInterval(boothOpen, boothClose, localTime);
    }

    public static bool IsWithinInterval(TimeOnly? open, TimeOnly? close, TimeOnly localTime)
    {
        if (!open.HasValue || !close.HasValue || open.Value == close.Value) return false;
        return open.Value < close.Value
            ? open.Value <= localTime && localTime < close.Value
            : localTime >= open.Value || localTime < close.Value;
    }
}
