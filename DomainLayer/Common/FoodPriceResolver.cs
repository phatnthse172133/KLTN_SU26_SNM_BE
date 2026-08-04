using DomainLayer.Entities;

namespace DomainLayer.Common;

public static class FoodPriceResolver
{
    public static decimal GetCurrentPrice(FoodItem foodItem, DateTime utcNow)
        => foodItem.FoodPrices
            .Where(price =>
                !price.IsDeleted &&
                (!price.StartDate.HasValue || price.StartDate.Value <= utcNow) &&
                (!price.EndDate.HasValue || price.EndDate.Value >= utcNow))
            .OrderByDescending(price => price.StartDate.HasValue)
            .ThenByDescending(price => price.StartDate)
            .ThenByDescending(price => price.CreatedAt)
            .Select(price => (decimal?)price.Price)
            .FirstOrDefault() ?? foodItem.Price;

    public static IQueryable<FoodItemWithEffectivePrice> WithCurrentPrice(
        IQueryable<FoodItem> foodItems,
        DateTime utcNow)
        => foodItems.Select(foodItem => new FoodItemWithEffectivePrice
        {
            FoodItem = foodItem,
            EffectivePrice = foodItem.FoodPrices
                .Where(price =>
                    !price.IsDeleted &&
                    (!price.StartDate.HasValue || price.StartDate.Value <= utcNow) &&
                    (!price.EndDate.HasValue || price.EndDate.Value >= utcNow))
                .OrderByDescending(price => price.StartDate.HasValue)
                .ThenByDescending(price => price.StartDate)
                .ThenByDescending(price => price.CreatedAt)
                .Select(price => (decimal?)price.Price)
                .FirstOrDefault() ?? foodItem.Price
        });
}

public sealed class FoodItemWithEffectivePrice
{
    public required FoodItem FoodItem { get; init; }
    public decimal EffectivePrice { get; init; }
}
