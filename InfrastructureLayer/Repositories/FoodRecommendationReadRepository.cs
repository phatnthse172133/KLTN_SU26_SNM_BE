using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.AI.V2.Services;
using DomainLayer.Common;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public sealed class FoodRecommendationReadRepository(SNMDbContext db) : IFoodRecommendationReadRepository
{
    public async Task<IReadOnlyCollection<FoodRecommendationCandidate>> GetCandidatesAsync(DateTime utcNow, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var foods = await db.FoodItems.AsNoTracking()
            .Where(food => !food.IsDeleted && food.IsAvailable && !food.Category.IsDeleted && food.Category.IsActive && food.Category.IsSelectable
                && food.Booth.Status == BoothStatus.Active && !food.Booth.NightMarket.IsDeleted
                && food.Booth.NightMarket.ModerationStatus == ModerationStatus.Active
                && food.Booth.NightMarket.Status == NightMarketStatus.Active)
            .OrderByDescending(food => food.IsFeatured).ThenByDescending(food => food.Booth.AverageRating).ThenBy(food => food.Id)
            .Take(limit)
            .Include(food => food.Category)
            .Include(food => food.Booth).ThenInclude(booth => booth.NightMarket)
            .Include(food => food.FoodPrices)
            .Include(food => food.Ingredients).ThenInclude(value => value.Ingredient)
            .Include(food => food.Allergens).ThenInclude(value => value.Allergen)
            .Include(food => food.DietaryAttributes).ThenInclude(value => value.DietaryAttribute)
            .Include(food => food.PreparationMethods).ThenInclude(value => value.PreparationMethod)
            .Include(food => food.TasteProfiles).ThenInclude(value => value.TasteProfile)
            .Include(food => food.Courses)
            .Include(food => food.DiningPurposes)
            .Include(food => food.AiProfile)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var boothIds = foods.Select(food => food.BoothId).Distinct().ToArray();
        var ratings = boothIds.Length == 0 ? [] : await db.Reviews.AsNoTracking()
            .Where(review => boothIds.Contains(review.BoothId) && review.IsVisible)
            .GroupBy(review => review.BoothId)
            .Select(group => new RatingRow(group.Key, (decimal?)group.Average(review => review.Rating), group.Count()))
            .ToListAsync(cancellationToken);
        var ratingMap = ratings.ToDictionary(value => value.BoothId);
        return foods.Select(food =>
        {
            var rating = ratingMap.GetValueOrDefault(food.BoothId);
            return new FoodRecommendationCandidate
            {
                FoodId = food.Id, FoodName = food.Name, Description = food.Description, ImageUrl = food.ThumbnailUrl,
                CategoryId = food.CategoryId, CategoryCode = food.Category.Code, CategoryName = food.Category.Name,
                CurrentPrice = FoodPriceResolver.GetCurrentPrice(food, utcNow), IsAvailable = food.IsAvailable,
                IsDeleted = food.IsDeleted, CategoryDeleted = food.Category.IsDeleted,
                CategoryIsActive = food.Category.IsActive, CategoryIsSelectable = food.Category.IsSelectable,
                BoothId = food.BoothId, BoothName = food.Booth.BoothName, BoothStatus = food.Booth.Status,
                BoothOpenTime = food.Booth.OpenTime, BoothCloseTime = food.Booth.CloseTime,
                MarketId = food.Booth.NightMarketId, MarketName = food.Booth.NightMarket.Name,
                MarketStatus = food.Booth.NightMarket.Status, MarketModerationStatus = food.Booth.NightMarket.ModerationStatus,
                MarketDeleted = food.Booth.NightMarket.IsDeleted, MarketOpenTime = food.Booth.NightMarket.OpeningHours,
                MarketCloseTime = food.Booth.NightMarket.ClosingHours, MarketLatitude = food.Booth.NightMarket.Latitude,
                MarketLongitude = food.Booth.NightMarket.Longitude, Rating = rating?.Average ?? food.Booth.AverageRating,
                ReviewCount = rating?.Count ?? 0, SearchText = food.AiProfile?.SearchText, SemanticProfileStatus = food.AiProfile?.Status,
                SpiceLevel = food.SpiceLevel, ServingTemperature = food.ServingTemperature,
                EstimatedServingCount = food.EstimatedServingCount, IsShareable = food.IsShareable,
                IngredientCodes = food.Ingredients.Select(value => value.Ingredient.Code).OrderBy(value => value).ToArray(),
                Allergens = food.Allergens.Select(value => new CandidateAllergen(value.Allergen.Code, value.DeclarationType, value.IsConfirmed, value.Source)).OrderBy(value => value.Code).ToArray(),
                DietaryAttributes = food.DietaryAttributes.Select(value => new CandidateDietaryAttribute(value.DietaryAttribute.Code, value.SuitabilityStatus, value.IsConfirmed, value.Source)).OrderBy(value => value.Code).ToArray(),
                PreparationMethodCodes = food.PreparationMethods.Select(value => value.PreparationMethod.Code).OrderBy(value => value).ToArray(),
                TasteCodes = food.TasteProfiles.Select(value => value.TasteProfile.Code).OrderBy(value => value).ToArray(),
                Courses = food.Courses.OrderByDescending(value => value.IsPrimary).ThenBy(value => value.Course).Select(value => value.Course).ToArray(),
                DiningPurposes = food.DiningPurposes.OrderBy(value => value.Purpose).Select(value => value.Purpose).ToArray()
            };
        }).ToArray();
    }

    private sealed record RatingRow(Guid BoothId, decimal? Average, int Count);
}
