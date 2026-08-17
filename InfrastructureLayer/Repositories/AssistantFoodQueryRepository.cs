using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public sealed class AssistantFoodQueryRepository(SNMDbContext db) : IAssistantFoodQueryRepository
{
    public async Task<IReadOnlyList<AssistantEligibleFood>> GetEligibleFoodsAsync(
        AssistantFoodQueryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = CustomerVisibleQuery();
        if (criteria.MarketId.HasValue)
            query = query.Where(item => item.Booth.NightMarketId == criteria.MarketId.Value);

        if (HasGps(criteria) && criteria.MaxDistanceMeters is > 0)
        {
            var lat = (decimal)criteria.Latitude!.Value;
            var lng = (decimal)criteria.Longitude!.Value;
            var max = criteria.MaxDistanceMeters.Value;
            var latDelta = (decimal)(max / 111_320d);
            var cosLat = Math.Cos(criteria.Latitude.Value * Math.PI / 180d);
            var lngDelta = (decimal)(max / Math.Max(1e-6, 111_320d * Math.Abs(cosLat)));
            query = query.Where(item =>
                item.Booth.NightMarket.Latitude != null
                && item.Booth.NightMarket.Longitude != null
                && item.Booth.NightMarket.Latitude >= lat - latDelta
                && item.Booth.NightMarket.Latitude <= lat + latDelta
                && item.Booth.NightMarket.Longitude >= lng - lngDelta
                && item.Booth.NightMarket.Longitude <= lng + lngDelta);
        }

        if (criteria.AvoidedIngredientIds.Count > 0)
        {
            var ids = criteria.AvoidedIngredientIds.ToArray();
            query = query.Where(item => !item.Ingredients.Any(value => ids.Contains(value.IngredientId)));
        }

        if (criteria.AvoidedTasteProfileIds.Count > 0)
        {
            var ids = criteria.AvoidedTasteProfileIds.ToArray();
            query = query.Where(item => !item.TasteProfiles.Any(value => ids.Contains(value.TasteProfileId)));
        }

        if (criteria.AllergenExclusionIds.Count > 0)
        {
            var ids = criteria.AllergenExclusionIds.ToArray();
            var treatMayContain = criteria.TreatMayContainAsHard;
            query = query.Where(item => !item.Allergens.Any(value =>
                ids.Contains(value.AllergenId)
                && (value.DeclarationType == AllergenDeclarationType.CONTAINS
                    || (treatMayContain && value.DeclarationType == AllergenDeclarationType.MAY_CONTAIN))));
        }

        foreach (var dietaryId in criteria.DietaryRequirementIds)
        {
            var requiredId = dietaryId;
            query = query.Where(item => item.DietaryAttributes.Any(value =>
                value.DietaryAttributeId == requiredId
                && value.SuitabilityStatus == DietarySuitabilityStatus.SUITABLE));
        }

        if (criteria.MaxSpiceLevel.HasValue && criteria.MaxSpiceLevel.Value != FoodSpiceLevel.UNKNOWN)
        {
            var allowed = Enum.GetValues<FoodSpiceLevel>()
                .Where(level => level != FoodSpiceLevel.UNKNOWN && (int)level <= (int)criteria.MaxSpiceLevel.Value)
                .ToArray();
            query = query.Where(item => allowed.Contains(item.SpiceLevel));
        }

        var entities = await query
            .Include(item => item.Booth).ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Booth).ThenInclude(booth => booth.Promotions)
            .Include(item => item.Category)
            .Include(item => item.FoodPrices)
            .Include(item => item.Ingredients).ThenInclude(item => item.Ingredient)
            .Include(item => item.Allergens).ThenInclude(item => item.Allergen)
            .Include(item => item.DietaryAttributes).ThenInclude(item => item.DietaryAttribute)
            .Include(item => item.TasteProfiles).ThenInclude(item => item.TasteProfile)
            .Include(item => item.PreparationMethods).ThenInclude(item => item.PreparationMethod)
            .Include(item => item.Courses)
            .Include(item => item.PromotionFoodItems).ThenInclude(item => item.Promotion)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var utcNow = criteria.UtcNow;
        var sales = await LoadSalesFactsAsync(entities.Select(item => item.Id).ToArray(), utcNow, cancellationToken);
        var result = new List<AssistantEligibleFood>(entities.Count);
        foreach (var item in entities)
        {
            var price = FoodPriceResolver.GetCurrentPrice(item, utcNow);
            if (criteria.BudgetMin.HasValue && price < criteria.BudgetMin.Value) continue;
            if (criteria.BudgetMax.HasValue && price > criteria.BudgetMax.Value) continue;

            double? distance = null;
            if (HasGps(criteria) && item.Booth.NightMarket.Latitude.HasValue && item.Booth.NightMarket.Longitude.HasValue)
            {
                distance = HaversineMeters(
                    criteria.Latitude!.Value,
                    criteria.Longitude!.Value,
                    (double)item.Booth.NightMarket.Latitude.Value,
                    (double)item.Booth.NightMarket.Longitude.Value);
                if (criteria.MaxDistanceMeters.HasValue && distance > criteria.MaxDistanceMeters.Value)
                    continue;
            }

            sales.TryGetValue(item.Id, out var fact);
            result.Add(new AssistantEligibleFood
            {
                FoodItem = item,
                EffectivePrice = price,
                DistanceMeters = distance,
                HasActivePromotion = HasActivePromotion(item, utcNow),
                SoldToday = fact.SoldToday,
                OrderCount = fact.OrderCount
            });
        }

        return result;
    }

    public Task<int> CountNotDeletedFoodItemsAsync(CancellationToken cancellationToken = default)
        => db.FoodItems.CountAsync(item => !item.IsDeleted, cancellationToken);

    public async Task<AssistantEligibleFood?> GetCurrentByIdAsync(
        Guid foodItemId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var item = await db.FoodItems
            .IgnoreQueryFilters()
            .Include(food => food.Booth).ThenInclude(booth => booth.NightMarket)
            .Include(food => food.Booth).ThenInclude(booth => booth.Promotions)
            .Include(food => food.Category)
            .Include(food => food.FoodPrices)
            .Include(food => food.Courses)
            .Include(food => food.PromotionFoodItems).ThenInclude(promo => promo.Promotion)
            .AsSplitQuery()
            .FirstOrDefaultAsync(food => food.Id == foodItemId, cancellationToken);
        if (item is null)
            return null;

        return new AssistantEligibleFood
        {
            FoodItem = item,
            EffectivePrice = FoodPriceResolver.GetCurrentPrice(item, utcNow),
            HasActivePromotion = HasActivePromotion(item, utcNow)
        };
    }

    private async Task<Dictionary<Guid, (int SoldToday, int OrderCount)>> LoadSalesFactsAsync(
        IReadOnlyCollection<Guid> foodIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var facts = new Dictionary<Guid, (int SoldToday, int OrderCount)>();
        if (foodIds.Count == 0)
            return facts;

        var ids = foodIds.Distinct().ToArray();
        var (startUtc, endUtc) = NightMarketAvailability.GetVietnamDayUtcRange(utcNow);
        var completed = OrderStatus.Completed;
        var rows = await db.OrderDetails
            .AsNoTracking()
            .Where(detail => ids.Contains(detail.FoodItemId) && detail.Order.Status == completed)
            .GroupBy(detail => detail.FoodItemId)
            .Select(group => new
            {
                FoodItemId = group.Key,
                OrderCount = group.Select(item => item.OrderId).Distinct().Count(),
                SoldToday = group
                    .Where(item => item.Order.CompletedAt != null
                        && item.Order.CompletedAt >= startUtc
                        && item.Order.CompletedAt < endUtc)
                    .Sum(item => (int?)item.Quantity) ?? 0
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
            facts[row.FoodItemId] = (row.SoldToday, row.OrderCount);
        return facts;
    }

    private IQueryable<FoodItem> CustomerVisibleQuery()
        => db.FoodItems.Where(item =>
            !item.IsDeleted &&
            item.IsAvailable &&
            !item.Category.IsDeleted &&
            item.Booth.Status == BoothStatus.Active &&
            !item.Booth.NightMarket.IsDeleted &&
            item.Booth.NightMarket.ModerationStatus == ModerationStatus.Active &&
            item.Booth.NightMarket.Status == NightMarketStatus.Active);

    private static bool HasGps(AssistantFoodQueryCriteria criteria)
        => criteria.Latitude is >= -90 and <= 90 && criteria.Longitude is >= -180 and <= 180;

    private static bool HasActivePromotion(FoodItem food, DateTime utcNow)
    {
        if (food.PromotionFoodItems.Any(item =>
            !item.Promotion.IsDeleted
            && item.Promotion.Status == PromotionStatus.Active
            && item.Promotion.StartDate <= utcNow
            && item.Promotion.EndDate >= utcNow))
            return true;

        return food.Booth.Promotions is not null && food.Booth.Promotions.Any(promotion =>
            !promotion.IsDeleted
            && promotion.Status == PromotionStatus.Active
            && promotion.StartDate <= utcNow
            && promotion.EndDate >= utcNow
            && promotion.Scope == PromotionScope.EntireBoothOrder);
    }

    internal static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6_371_000d;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * radius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
