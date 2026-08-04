using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class FoodItemRepository : GenericRepository<FoodItem>, IFoodItemRepository
{
    public FoodItemRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<CustomerFoodReadModel>> GetCustomerPagedAsync(
        Guid? marketId,
        Guid? boothId,
        Guid? categoryId,
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        bool availableOnly,
        DateTime utcNow,
        TimeOnly localTime,
        int page,
        int pageSize,
        string sort,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            return await GetCustomerPagedInMemoryAsync(
                marketId, boothId, categoryId, search, minPrice, maxPrice,
                availableOnly, utcNow, localTime, page, pageSize, sort, cancellationToken);

        var query = CustomerVisibleQuery();

        if (marketId.HasValue)
            query = query.Where(item => item.Booth.NightMarketId == marketId.Value);
        if (boothId.HasValue)
            query = query.Where(item => item.BoothId == boothId.Value);
        if (categoryId.HasValue)
            query = query.Where(item => item.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(item => item.Name.ToLower().Contains(keyword)
                || (item.AiProfile != null && item.AiProfile.SearchText.ToLower().Contains(keyword)));
        }

        if (availableOnly)
            query = query.Where(IsCustomerOrderableAt(localTime));

        var priced = FoodPriceResolver.WithCurrentPrice(query, utcNow);
        if (minPrice.HasValue)
            priced = priced.Where(item => item.EffectivePrice >= minPrice.Value);
        if (maxPrice.HasValue)
            priced = priced.Where(item => item.EffectivePrice <= maxPrice.Value);

        var totalCount = await priced.CountAsync(cancellationToken);
        priced = sort.ToLowerInvariant() switch
        {
            "name" => priced.OrderBy(item => item.FoodItem.Name).ThenBy(item => item.FoodItem.Id),
            "priceasc" => priced.OrderBy(item => item.EffectivePrice).ThenBy(item => item.FoodItem.Name).ThenBy(item => item.FoodItem.Id),
            "pricedesc" => priced.OrderByDescending(item => item.EffectivePrice).ThenBy(item => item.FoodItem.Name).ThenBy(item => item.FoodItem.Id),
            _ => priced.OrderByDescending(item => item.FoodItem.IsFeatured).ThenBy(item => item.FoodItem.Name).ThenBy(item => item.FoodItem.Id)
        };

        var items = await ProjectCustomer(priced, includeImages: false)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerFoodReadModel>(items, totalCount);
    }

    public async Task<CustomerFoodReadModel?> GetCustomerByIdAsync(
        Guid foodItemId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var food = await CustomerVisibleQuery()
            .Include(item => item.Booth).ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Category).Include(item => item.FoodPrices).Include(item => item.FoodImages)
            .Include(item => item.Courses)
            .Include(item => item.Ingredients).ThenInclude(x => x.Ingredient)
            .Include(item => item.Allergens).ThenInclude(x => x.Allergen)
            .Include(item => item.DietaryAttributes).ThenInclude(x => x.DietaryAttribute)
            .Include(item => item.PreparationMethods).ThenInclude(x => x.PreparationMethod)
            .Include(item => item.TasteProfiles).ThenInclude(x => x.TasteProfile)
            .AsSplitQuery().FirstOrDefaultAsync(item => item.Id == foodItemId, cancellationToken);
        return food is null ? null : ToCustomerReadModel(food, utcNow, includeImages: true);
    }

    public async Task<PagedResult<NightMarketFoodCustomerReadModel>> GetCustomerByNightMarketPagedAsync(
        Guid nightMarketId,
        DateTime utcNow,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = CustomerVisibleQuery().Where(item => item.Booth.NightMarketId == nightMarketId);

        var totalCount = await query.CountAsync(cancellationToken);
        var foodItems = await query
            .OrderByDescending(item => item.IsFeatured)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Category)
            .Include(item => item.FoodPrices)
            .AsSplitQuery()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = foodItems
            .Select(item => new NightMarketFoodCustomerReadModel(
                item.Id,
                item.BoothId,
                item.Booth.BoothName,
                item.CategoryId,
                item.Category.Name,
                item.Name,
                item.Description,
                FoodPriceResolver.GetCurrentPrice(item, utcNow),
                item.ThumbnailUrl,
                item.IsAvailable,
                item.Booth.OpenTime,
                item.Booth.CloseTime,
                item.Booth.NightMarket.OpeningHours,
                item.Booth.NightMarket.ClosingHours,
                item.Booth.NightMarket.Status == NightMarketStatus.Active,
                item.IsFeatured))
            .ToList();

        return new PagedResult<NightMarketFoodCustomerReadModel>(items, totalCount);
    }

    public async Task<PagedResult<FoodItem>> GetMenuByBoothPagedAsync(
        Guid boothId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(item => item.Category)
            .Include(item => item.FoodItemTags)
            .Include(item => item.Courses)
            .Include(item => item.Ingredients).ThenInclude(item => item.Ingredient)
            .Include(item => item.Allergens).ThenInclude(item => item.Allergen)
            .Include(item => item.DietaryAttributes).ThenInclude(item => item.DietaryAttribute)
            .Include(item => item.PreparationMethods).ThenInclude(item => item.PreparationMethod)
            .Include(item => item.TasteProfiles).ThenInclude(item => item.TasteProfile)
            .Include(item => item.AiProfile)
            .AsSplitQuery()
            .Where(item => item.BoothId == boothId && !item.IsDeleted);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.IsFeatured)
            .ThenByDescending(item => item.IsAvailable)
            .ThenBy(item => item.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FoodItem>(items, totalCount);
    }

    public async Task<FoodItem?> GetByBoothAsync(Guid boothId, Guid foodItemId)
        => await _dbSet
            .Include(item => item.Category)
            .Include(item => item.FoodItemTags)
            .Include(item => item.Courses)
            .Include(item => item.Ingredients).ThenInclude(item => item.Ingredient)
            .Include(item => item.Allergens).ThenInclude(item => item.Allergen)
            .Include(item => item.DietaryAttributes).ThenInclude(item => item.DietaryAttribute)
            .Include(item => item.PreparationMethods).ThenInclude(item => item.PreparationMethod)
            .Include(item => item.TasteProfiles).ThenInclude(item => item.TasteProfile)
            .Include(item => item.AiProfile)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == foodItemId && item.BoothId == boothId && !item.IsDeleted);

    public Task<FoodItem?> GetForCartAsync(
        Guid foodItemId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .IgnoreQueryFilters()
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Category)
            .Include(item => item.FoodPrices)
            .FirstOrDefaultAsync(item => item.Id == foodItemId, cancellationToken);

    public Task<FoodItem?> GetWithTagsAsync(
        Guid foodItemId,
        CancellationToken cancellationToken = default)
        => ActiveQuery()
            .Include(item => item.Booth)
            .Include(item => item.FoodItemTags)
            .FirstOrDefaultAsync(item => item.Id == foodItemId, cancellationToken);

    public async Task<IReadOnlyCollection<FoodItem>> GetActiveByIdsAndBoothAsync(
        Guid boothId,
        IReadOnlyCollection<Guid> foodItemIds,
        CancellationToken cancellationToken = default)
        => await ActiveQuery()
            .Where(item => item.BoothId == boothId && foodItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<FoodItem>> GetAiCandidatesAsync(
        Guid? nightMarketId,
        int maxCandidates,
        CancellationToken cancellationToken = default)
    {
        if (maxCandidates is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(maxCandidates));

        var query = ActiveQuery()
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.FoodPrices)
            .Include(item => item.FoodItemTags)
                .ThenInclude(foodItemTag => foodItemTag.FoodTag)
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.Zone)
            .Where(item =>
                item.IsAvailable
                && !item.Category.IsDeleted
                && item.Booth.Status == BoothStatus.Active
                && item.Booth.NightMarket.Status == NightMarketStatus.Active
                && item.Booth.NightMarket.ModerationStatus == ModerationStatus.Active
                && !item.Booth.NightMarket.IsDeleted);

        if (nightMarketId.HasValue)
        {
            query = query.Where(item => item.Booth.NightMarketId == nightMarketId.Value);
        }

        return await query
            .OrderByDescending(item => item.IsFeatured)
            .ThenByDescending(item => item.Booth.AverageRating)
            .ThenBy(item => item.Id)
            .Take(maxCandidates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FoodItem>> GetAiOrderableCandidatesAsync(
        Guid? nightMarketId,
        TimeOnly localTime,
        int maxCandidates,
        CancellationToken cancellationToken = default)
    {
        if (maxCandidates is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(maxCandidates));

        var query = ActiveQuery()
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.FoodPrices)
            .Include(item => item.FoodItemTags)
                .ThenInclude(foodItemTag => foodItemTag.FoodTag)
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.Zone)
            .Where(item =>
                !item.Category.IsDeleted
                && item.Booth.Status == BoothStatus.Active
                && item.Booth.NightMarket.ModerationStatus == ModerationStatus.Active
                && !item.Booth.NightMarket.IsDeleted)
            .Where(IsCustomerOrderableAt(localTime));

        if (nightMarketId.HasValue)
            query = query.Where(item => item.Booth.NightMarketId == nightMarketId.Value);

        return await query
            .OrderByDescending(item => item.IsFeatured)
            .ThenByDescending(item => item.Booth.AverageRating)
            .ThenBy(item => item.Id)
            .Take(maxCandidates)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FoodItem>> GetAllFoodItemsByIdsAsync(List<Guid> foodItemIds)
    {
        return await _dbSet
            .IgnoreQueryFilters()
            .Include(item => item.Booth)
                .ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Category)
            .Include(item => item.FoodPrices)
            .AsSplitQuery()
            .Where(item => foodItemIds.Contains(item.Id))
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<FoodItem>> GetSemanticProfileBatchAsync(Guid? foodItemId, int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var query = ActiveQuery().Include(x => x.Category).Include(x => x.Courses)
            .Include(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .Include(x => x.DietaryAttributes).ThenInclude(x => x.DietaryAttribute)
            .Include(x => x.PreparationMethods).ThenInclude(x => x.PreparationMethod)
            .Include(x => x.TasteProfiles).ThenInclude(x => x.TasteProfile)
            .Include(x => x.AiProfile).AsSplitQuery();
        if (foodItemId.HasValue) query = query.Where(x => x.Id == foodItemId.Value);
        return await query.OrderBy(x => x.Id).Take(batchSize).ToListAsync(cancellationToken);
    }

    private IQueryable<FoodItem> CustomerVisibleQuery()
        => ActiveQuery().AsNoTracking().Where(item =>
            item.IsAvailable &&
            !item.Category.IsDeleted &&
            item.Booth.Status == BoothStatus.Active &&
            !item.Booth.NightMarket.IsDeleted &&
            item.Booth.NightMarket.ModerationStatus == ModerationStatus.Active &&
            item.Booth.NightMarket.Status == NightMarketStatus.Active);

    private static Expression<Func<FoodItem, bool>> IsCustomerOrderableAt(TimeOnly localTime)
        => item =>
            item.IsAvailable &&
            item.Booth.NightMarket.Status == NightMarketStatus.Active &&
            item.Booth.NightMarket.OpeningHours.HasValue &&
            item.Booth.NightMarket.ClosingHours.HasValue &&
            item.Booth.NightMarket.OpeningHours.Value != item.Booth.NightMarket.ClosingHours.Value &&
            (item.Booth.NightMarket.OpeningHours.Value < item.Booth.NightMarket.ClosingHours.Value
                ? item.Booth.NightMarket.OpeningHours.Value <= localTime && localTime < item.Booth.NightMarket.ClosingHours.Value
                : localTime >= item.Booth.NightMarket.OpeningHours.Value || localTime < item.Booth.NightMarket.ClosingHours.Value) &&
            ((!item.Booth.OpenTime.HasValue && !item.Booth.CloseTime.HasValue) ||
             (item.Booth.OpenTime.HasValue && item.Booth.CloseTime.HasValue &&
              item.Booth.OpenTime.Value != item.Booth.CloseTime.Value &&
              (item.Booth.OpenTime.Value < item.Booth.CloseTime.Value
                  ? item.Booth.OpenTime.Value <= localTime && localTime < item.Booth.CloseTime.Value
                  : localTime >= item.Booth.OpenTime.Value || localTime < item.Booth.CloseTime.Value)));

    private static IQueryable<CustomerFoodReadModel> ProjectCustomer(
        IQueryable<FoodItemWithEffectivePrice> query,
        bool includeImages)
        => query.Select(priced => new CustomerFoodReadModel(
            priced.FoodItem.Id,
            priced.FoodItem.BoothId,
            priced.FoodItem.Booth.BoothName,
            priced.FoodItem.Booth.ThumbnailUrl,
            priced.FoodItem.Booth.NightMarketId,
            priced.FoodItem.Booth.NightMarket.Name,
            priced.FoodItem.Booth.NightMarket.Address,
            priced.FoodItem.CategoryId,
            priced.FoodItem.Category.Name,
            priced.FoodItem.Name,
            priced.FoodItem.Description,
            priced.FoodItem.Price,
            priced.EffectivePrice,
            priced.FoodItem.ThumbnailUrl,
            priced.FoodItem.IsAvailable,
            priced.FoodItem.IsFeatured,
            priced.FoodItem.Booth.OpenTime,
            priced.FoodItem.Booth.CloseTime,
            priced.FoodItem.Booth.NightMarket.OpeningHours,
            priced.FoodItem.Booth.NightMarket.ClosingHours,
            priced.FoodItem.Booth.NightMarket.Status == NightMarketStatus.Active,
            includeImages
                ? priced.FoodItem.FoodImages.OrderBy(image => image.DisplayOrder).ThenBy(image => image.Id).Select(image => image.ImageUrl).ToList()
                : new List<string>())
        {
            PrimaryCourse = priced.FoodItem.Courses.Where(x => x.IsPrimary).Select(x => (FoodCourse?)x.Course).FirstOrDefault(),
            SpiceLevel = priced.FoodItem.SpiceLevel,
            ServingTemperature = priced.FoodItem.ServingTemperature,
            EstimatedServingCount = priced.FoodItem.EstimatedServingCount,
            ServingSizeDescription = priced.FoodItem.ServingSizeDescription,
            IsShareable = priced.FoodItem.IsShareable
        });

    private async Task<PagedResult<CustomerFoodReadModel>> GetCustomerPagedInMemoryAsync(
        Guid? marketId,
        Guid? boothId,
        Guid? categoryId,
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        bool availableOnly,
        DateTime utcNow,
        TimeOnly localTime,
        int page,
        int pageSize,
        string sort,
        CancellationToken cancellationToken)
    {
        var entities = await CustomerVisibleQuery()
            .Include(item => item.Booth).ThenInclude(booth => booth.NightMarket)
            .Include(item => item.Category)
            .Include(item => item.FoodPrices)
            .Include(item => item.Courses)
            .Include(item => item.AiProfile)
            .ToListAsync(cancellationToken);

        IEnumerable<CustomerFoodReadModel> query = entities
            .Where(item => !marketId.HasValue || item.Booth.NightMarketId == marketId.Value)
            .Where(item => !boothId.HasValue || item.BoothId == boothId.Value)
            .Where(item => !categoryId.HasValue || item.CategoryId == categoryId.Value)
            .Where(item => string.IsNullOrWhiteSpace(search)
                || item.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || (item.AiProfile?.SearchText.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(item => ToCustomerReadModel(item, utcNow, includeImages: false))
            .Where(item => !minPrice.HasValue || item.EffectivePrice >= minPrice.Value)
            .Where(item => !maxPrice.HasValue || item.EffectivePrice <= maxPrice.Value)
            .Where(item => !availableOnly || (item.IsAvailable &&
                ApplicationLayer.Services.CustomerDiscovery.CustomerAvailability.IsOpenNow(item, localTime)));

        var totalCount = query.Count();
        query = sort.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(item => item.Name).ThenBy(item => item.Id),
            "priceasc" => query.OrderBy(item => item.EffectivePrice).ThenBy(item => item.Name).ThenBy(item => item.Id),
            "pricedesc" => query.OrderByDescending(item => item.EffectivePrice).ThenBy(item => item.Name).ThenBy(item => item.Id),
            _ => query.OrderByDescending(item => item.IsFeatured).ThenBy(item => item.Name).ThenBy(item => item.Id)
        };

        return new PagedResult<CustomerFoodReadModel>(
            query.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            totalCount);
    }

    private static CustomerFoodReadModel ToCustomerReadModel(
        FoodItem item,
        DateTime utcNow,
        bool includeImages)
        => new(
            item.Id,
            item.BoothId,
            item.Booth.BoothName,
            item.Booth.ThumbnailUrl,
            item.Booth.NightMarketId,
            item.Booth.NightMarket.Name,
            item.Booth.NightMarket.Address,
            item.CategoryId,
            item.Category.Name,
            item.Name,
            item.Description,
            item.Price,
            FoodPriceResolver.GetCurrentPrice(item, utcNow),
            item.ThumbnailUrl,
            item.IsAvailable,
            item.IsFeatured,
            item.Booth.OpenTime,
            item.Booth.CloseTime,
            item.Booth.NightMarket.OpeningHours,
            item.Booth.NightMarket.ClosingHours,
            item.Booth.NightMarket.Status == NightMarketStatus.Active,
            includeImages
                ? item.FoodImages.OrderBy(image => image.DisplayOrder).ThenBy(image => image.Id).Select(image => image.ImageUrl).ToList()
                : new List<string>())
        {
            PrimaryCourse = item.Courses.SingleOrDefault(x => x.IsPrimary)?.Course,
            SpiceLevel = item.SpiceLevel,
            ServingTemperature = item.ServingTemperature,
            EstimatedServingCount = item.EstimatedServingCount,
            ServingSizeDescription = item.ServingSizeDescription,
            IsShareable = item.IsShareable,
            SemanticMetadata = new CustomerFoodSemanticReadModel
            {
                PrimaryCourse = item.Courses.SingleOrDefault(x => x.IsPrimary)?.Course,
                SupportedCourses = item.Courses.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Course).Select(x => x.Course).ToArray(),
                Ingredients = item.Ingredients.OrderBy(x => x.Ingredient.Code).Select(x => new CustomerSemanticCatalogReadModel(x.IngredientId, x.Ingredient.Code, x.Ingredient.Name)).ToArray(),
                Allergens = item.Allergens.OrderBy(x => x.Allergen.Code).Select(x => new CustomerAllergenReadModel(x.AllergenId, x.Allergen.Code, x.Allergen.Name, x.DeclarationType, x.IsConfirmed, x.Source)).ToArray(),
                Dietary = item.DietaryAttributes.OrderBy(x => x.DietaryAttribute.Code).Select(x => new CustomerDietaryReadModel(x.DietaryAttributeId, x.DietaryAttribute.Code, x.DietaryAttribute.Name, x.SuitabilityStatus, x.IsConfirmed, x.Source)).ToArray(),
                Preparations = item.PreparationMethods.OrderBy(x => x.PreparationMethod.Code).Select(x => new CustomerSemanticCatalogReadModel(x.PreparationMethodId, x.PreparationMethod.Code, x.PreparationMethod.Name)).ToArray(),
                Tastes = item.TasteProfiles.OrderBy(x => x.TasteProfile.Code).Select(x => new CustomerSemanticCatalogReadModel(x.TasteProfileId, x.TasteProfile.Code, x.TasteProfile.Name)).ToArray()
            },
            Tags = BuildDeprecatedTags(item)
        };

    private static IReadOnlyCollection<CustomerFoodTagReadModel> BuildDeprecatedTags(FoodItem item)
    {
        var tags = new List<CustomerFoodTagReadModel>();
        tags.AddRange(item.Ingredients.Select(x => new CustomerFoodTagReadModel(x.IngredientId, x.Ingredient.Code, x.Ingredient.Name, FoodTagGroup.Ingredient)));
        tags.AddRange(item.DietaryAttributes.Select(x => new CustomerFoodTagReadModel(x.DietaryAttributeId, x.DietaryAttribute.Code, x.DietaryAttribute.Name, FoodTagGroup.Dietary)));
        tags.AddRange(item.PreparationMethods.Select(x => new CustomerFoodTagReadModel(x.PreparationMethodId, x.PreparationMethod.Code, x.PreparationMethod.Name, FoodTagGroup.CookingMethod)));
        tags.AddRange(item.TasteProfiles.Select(x => new CustomerFoodTagReadModel(x.TasteProfileId, x.TasteProfile.Code, x.TasteProfile.Name, FoodTagGroup.Taste)));
        return tags.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
    }
}
