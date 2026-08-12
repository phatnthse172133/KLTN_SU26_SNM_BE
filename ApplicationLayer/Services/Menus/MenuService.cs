using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.AI.Services;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using DomainLayer.Enums;

namespace ApplicationLayer.Services.Menus;

public class MenuService : IMenuService
{
    private readonly IBoothRepository _booths;
    private readonly IFoodCategoryRepository _categories;
    private readonly IFoodItemRepository _foodItems;
    private readonly IFoodTagRepository _foodTags;
    private readonly IMapper _mapper;
    private readonly IFoodSemanticMetadataRepository? _semanticMetadata;
    private readonly IFoodAiProfileGenerator? _profileGenerator;
    private readonly ILegacyFoodTagMetadataAdapter? _legacyAdapter;
    private readonly IFoodAiProfileEnrichmentService? _profileEnrichment;

    public MenuService(
        IBoothRepository booths,
        IFoodCategoryRepository categories,
        IFoodItemRepository foodItems,
        IFoodTagRepository foodTags,
        IMapper mapper)
    {
        _booths = booths;
        _categories = categories;
        _foodItems = foodItems;
        _foodTags = foodTags;
        _mapper = mapper;
    }

    public MenuService(
        IBoothRepository booths,
        IFoodCategoryRepository categories,
        IFoodItemRepository foodItems,
        IFoodTagRepository foodTags,
        IMapper mapper,
        IFoodSemanticMetadataRepository semanticMetadata,
        IFoodAiProfileGenerator profileGenerator,
        ILegacyFoodTagMetadataAdapter legacyAdapter,
        IFoodAiProfileEnrichmentService? profileEnrichment = null)
        : this(booths, categories, foodItems, foodTags, mapper)
        => (_semanticMetadata, _profileGenerator, _legacyAdapter, _profileEnrichment)
            = (semanticMetadata, profileGenerator, legacyAdapter, profileEnrichment);

    public async Task<ApiResponse<PaginationResp<FoodItemResponse>>> GetMyBoothMenuAsync(
        Guid ownerId,
        Guid boothId,
        PaginationReq pagination,
        CancellationToken cancellationToken = default)
    {
        var ownershipError = await ValidateBoothOwnershipAsync(ownerId, boothId);
        if (ownershipError is not null)
            throw ToBoothAccessException(ownershipError);

        var page = await _foodItems.GetMenuByBoothPagedAsync(
            boothId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<FoodItemResponse>>.SuccessResponse(
            _mapper.MapPage<FoodItem, FoodItemResponse>(page, pagination));
    }

    public async Task<ApiResponse<FoodItemResponse>> CreateFoodItemAsync(Guid ownerId, Guid boothId, CreateFoodItemRequest request, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null)
            throw ToBoothAccessException(managementError);

        var (validationError, category, tags) = await ValidateFoodItemRequestAsync(boothId, request, cancellationToken);
        if (validationError is not null)
            throw validationError.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? AppException.NotFound(validationError)
                : AppException.BadRequest(validationError);

        var now = DateTime.UtcNow;
        var foodItem = _mapper.Map<FoodItem>(request);
        foodItem.Id = Guid.NewGuid();
        foodItem.BoothId = boothId;
        foodItem.Category = category!;
        foodItem.IsDeleted = false;
        foodItem.CreatedAt = now;
        foodItem.UpdatedAt = now;
        SetTags(foodItem, tags!, now);
        if (_legacyAdapter is not null)
        {
            await _legacyAdapter.ApplySupportedAsync(foodItem, tags!, now, cancellationToken);
            _profileGenerator!.Rebuild(foodItem, now);
        }

        await _foodItems.AddAsync(foodItem);
        await _foodItems.SaveChangesAsync();
        await TryEnrichAiProfileAsync(foodItem.Id, cancellationToken);

        return ApiResponse<FoodItemResponse>.SuccessResponse(_mapper.Map<FoodItemResponse>(foodItem), "Food item created successfully.");
    }

    public async Task<ApiResponse<FoodItemResponse>> UpdateFoodItemAsync(Guid ownerId, Guid boothId, Guid foodItemId, UpdateFoodItemRequest request, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null)
            throw ToBoothAccessException(managementError);

        var foodItem = await _foodItems.GetByBoothAsync(boothId, foodItemId);
        if (foodItem is null)
            throw AppException.NotFound("Food item was not found.");

        var (validationError, category, tags) = await ValidateFoodItemRequestAsync(boothId, request, cancellationToken);
        if (validationError is not null)
            throw validationError.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? AppException.NotFound(validationError)
                : AppException.BadRequest(validationError);

        _mapper.Map(request, foodItem);
        foodItem.Category = category!;
        foodItem.UpdatedAt = DateTime.UtcNow;
        SetTags(foodItem, tags!, foodItem.UpdatedAt);
        if (_legacyAdapter is not null)
        {
            await _legacyAdapter.ApplySupportedAsync(foodItem, tags!, foodItem.UpdatedAt, cancellationToken);
            _profileGenerator!.Rebuild(foodItem, foodItem.UpdatedAt);
        }

        _foodItems.Update(foodItem);
        await _foodItems.SaveChangesAsync();
        await TryEnrichAiProfileAsync(foodItem.Id, cancellationToken);

        return ApiResponse<FoodItemResponse>.SuccessResponse(_mapper.Map<FoodItemResponse>(foodItem), "Food item updated successfully.");
    }

    public async Task<ApiResponse<FoodItemV2Response>> CreateFoodItemV2Async(Guid ownerId, Guid boothId, CreateFoodItemV2Request request, CancellationToken cancellationToken = default)
    {
        EnsureV2Dependencies();
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null) throw ToBoothAccessException(managementError);
        var category = await ValidateBaseV2Async(boothId, request, cancellationToken);
        var metadata = await LoadAndValidateMetadataAsync(request, cancellationToken);
        var now = DateTime.UtcNow;
        var food = new FoodItem
        {
            Id = Guid.NewGuid(), BoothId = boothId, CategoryId = category.Id, Category = category,
            Name = request.Name.Trim(), Description = request.Description?.Trim(), Price = request.Price,
            ThumbnailUrl = request.ThumbnailUrl, IsAvailable = request.IsAvailable, IsFeatured = request.IsFeatured,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        };
        ApplyNormalizedMetadata(food, request, metadata, now);
        _profileGenerator!.Rebuild(food, now);
        await _foodItems.AddAsync(food);
        await _foodItems.SaveChangesAsync();
        await TryEnrichAiProfileAsync(food.Id, cancellationToken);
        return ApiResponse<FoodItemV2Response>.SuccessResponse(MapV2(food), "Food item created with normalized metadata.");
    }

    public async Task<ApiResponse<FoodItemV2Response>> UpdateFoodItemV2Async(Guid ownerId, Guid boothId, Guid foodItemId, UpdateFoodItemV2Request request, CancellationToken cancellationToken = default)
    {
        EnsureV2Dependencies();
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null) throw ToBoothAccessException(managementError);
        var food = await _foodItems.GetByBoothAsync(boothId, foodItemId) ?? throw AppException.NotFound("Food item was not found.");
        var category = await ValidateBaseV2Async(boothId, request, cancellationToken);
        var metadata = await LoadAndValidateMetadataAsync(request, cancellationToken);
        var now = DateTime.UtcNow;
        food.CategoryId = category.Id; food.Category = category; food.Name = request.Name.Trim(); food.Description = request.Description?.Trim();
        food.Price = request.Price; food.ThumbnailUrl = request.ThumbnailUrl; food.IsAvailable = request.IsAvailable; food.IsFeatured = request.IsFeatured; food.UpdatedAt = now;
        ApplyNormalizedMetadata(food, request, metadata, now);
        _profileGenerator!.Rebuild(food, now);
        _foodItems.Update(food);
        await _foodItems.SaveChangesAsync();
        await TryEnrichAiProfileAsync(food.Id, cancellationToken);
        return ApiResponse<FoodItemV2Response>.SuccessResponse(MapV2(food), "Food item normalized metadata replaced successfully.");
    }

    public async Task<ApiResponse<FoodItemResponse>> UpdateAvailabilityAsync(Guid ownerId, Guid boothId, Guid foodItemId, UpdateFoodAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null)
            throw ToBoothAccessException(managementError);

        var foodItem = await _foodItems.GetByBoothAsync(boothId, foodItemId);
        if (foodItem is null)
            throw AppException.NotFound("Food item was not found.");

        foodItem.IsAvailable = request.IsAvailable;
        foodItem.UpdatedAt = DateTime.UtcNow;

        _foodItems.Update(foodItem);
        await _foodItems.SaveChangesAsync();

        return ApiResponse<FoodItemResponse>.SuccessResponse(_mapper.Map<FoodItemResponse>(foodItem), request.IsAvailable ? "Food item is now available." : "Food item is now unavailable.");
    }

    public async Task<ApiResponse<FoodItemResponse>> UpdateFeaturedAsync(Guid ownerId, Guid boothId, Guid foodItemId, UpdateFoodFeaturedRequest request, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null)
            throw ToBoothAccessException(managementError);

        var foodItem = await _foodItems.GetByBoothAsync(boothId, foodItemId);
        if (foodItem is null)
            throw AppException.NotFound("Food item was not found.");

        foodItem.IsFeatured = request.IsFeatured;
        foodItem.UpdatedAt = DateTime.UtcNow;

        _foodItems.Update(foodItem);
        await _foodItems.SaveChangesAsync();

        return ApiResponse<FoodItemResponse>.SuccessResponse(_mapper.Map<FoodItemResponse>(foodItem), request.IsFeatured ? "Food item marked as featured." : "Food item removed from featured list.");
    }

    public async Task<ApiResponse<object>> DeleteFoodItemAsync(Guid ownerId, Guid boothId, Guid foodItemId, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null)
            throw ToBoothAccessException(managementError);

        var foodItem = await _foodItems.GetByBoothAsync(boothId, foodItemId);
        if (foodItem is null)
            throw AppException.NotFound("Food item was not found.");

        foodItem.IsAvailable = false;
        foodItem.IsFeatured = false;
        foodItem.UpdatedAt = DateTime.UtcNow;
        _foodItems.Delete(foodItem);
        await _foodItems.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { foodItem.Id }, "Food item deleted successfully.");
    }

    private async Task<string?> ValidateBoothOwnershipAsync(Guid ownerId, Guid boothId)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null)
            return "Booth was not found.";
        return booth.BoothOwnerId == ownerId ? null : "You do not have permission to manage this booth.";
    }

    private async Task<string?> ValidateBoothManagementAsync(Guid ownerId, Guid boothId)
    {
        var booth = await _booths.GetOwnedBoothAsync(ownerId, boothId);
        if (booth is null)
        {
            return await _booths.AnyAsync(b => b.Id == boothId)
                ? "You do not have permission to manage this booth."
                : "Booth was not found.";
        }

        if (booth.Status is BoothStatus.Banned or BoothStatus.Inactive)
            return "This booth cannot manage menu items in its current status.";

        return null;
    }

    private async Task<(string? Error, FoodCategory? Category, IReadOnlyCollection<FoodTag>? Tags)> ValidateFoodItemRequestAsync(
        Guid boothId, CreateFoodItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ("Food item name is required.", null, null);

        if (request.Price <= 0)
            return ("Food item price must be greater than zero.", null, null);

        var category = await _categories.GetActiveByBoothAsync(boothId, request.CategoryId);
        if (category is null)
            return ("Food category was not found or is not selectable.", null, null);

        if (request.TagIds.Count != request.TagIds.Distinct().Count())
            return ("Food tag IDs must not contain duplicates.", null, null);

        var tagIds = request.TagIds.ToList();
        var tags = await _foodTags.GetActiveByIdsAsync(tagIds, cancellationToken);
        if (tags.Count != tagIds.Count)
            return ("One or more food tags are invalid or inactive.", null, null);

        FoodTagSelectionPolicy.Validate(tags);

        return (null, category, tags);
    }

    private static void SetTags(FoodItem foodItem, IReadOnlyCollection<FoodTag> tags, DateTime now)
    {
        var requestedIds = tags.Select(tag => tag.Id).ToHashSet();
        foreach (var existing in foodItem.FoodItemTags.Where(item => !requestedIds.Contains(item.FoodTagId)).ToList())
            foodItem.FoodItemTags.Remove(existing);

        var existingIds = foodItem.FoodItemTags.Select(item => item.FoodTagId).ToHashSet();
        foreach (var tag in tags.Where(tag => !existingIds.Contains(tag.Id)))
        {
            foodItem.FoodItemTags.Add(new FoodItemTag
            {
                FoodItemId = foodItem.Id,
                FoodTagId = tag.Id,
                CreatedAt = now
            });
        }
    }

    private static AppException ToBoothAccessException(string message)
        => message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            ? AppException.Forbidden(message)
            : message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? AppException.NotFound(message)
                : AppException.BadRequest(message);

    private void EnsureV2Dependencies()
    {
        if (_semanticMetadata is null || _profileGenerator is null)
            throw new InvalidOperationException("Normalized menu dependencies are not configured.");
    }

    private async Task TryEnrichAiProfileAsync(Guid foodItemId, CancellationToken cancellationToken)
    {
        if (_profileEnrichment is null) return;
        try
        {
            await _profileEnrichment.EnrichOneAsync(foodItemId, cancellationToken);
        }
        catch
        {
            // Food CRUD must succeed even when post-commit enrichment fails.
        }
    }

    private async Task<FoodCategory> ValidateBaseV2Async(Guid boothId, CreateFoodItemV2Request request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw AppException.BadRequest("Food item name is required.");
        if (request.Price <= 0) throw AppException.BadRequest("Food item price must be greater than zero.");
        if (request.EstimatedServingCount <= 0) throw AppException.BadRequest("Estimated serving count must be greater than zero.");
        if (!request.PrimaryCourse.HasValue || !Enum.IsDefined(request.PrimaryCourse.Value)) throw AppException.BadRequest("Primary course is required and must be valid.");
        if (request.AdditionalCourses.Any(course => !Enum.IsDefined(course))) throw AppException.BadRequest("One or more additional courses are invalid.");
        if (request.AdditionalCourses.Contains(request.PrimaryCourse.Value)) throw AppException.BadRequest("Primary course cannot be repeated as an additional course.");
        if (request.AdditionalCourses.Count != request.AdditionalCourses.Distinct().Count()) throw AppException.BadRequest("Course values must not contain duplicates.");
        if (!Enum.IsDefined(request.SpiceLevel) || (request.ServingTemperature.HasValue && !Enum.IsDefined(request.ServingTemperature.Value))) throw AppException.BadRequest("Semantic enum value is invalid.");
        if (request.ConfirmedAllergenDeclarations.Any(x => !Enum.IsDefined(x.DeclarationType))) throw AppException.BadRequest("Allergen declaration type is invalid.");
        return await _categories.GetActiveByBoothAsync(boothId, request.CategoryId)
            ?? throw AppException.NotFound("Food category was not found or is not selectable.");
    }

    private async Task<MetadataSelection> LoadAndValidateMetadataAsync(CreateFoodItemV2Request request, CancellationToken ct)
    {
        static void EnsureUnique(IEnumerable<Guid> values, string name)
        {
            var array = values.ToArray();
            if (array.Length != array.Distinct().Count()) throw AppException.BadRequest($"{name} must not contain duplicates.");
        }
        EnsureUnique(request.IngredientIds, "Ingredient IDs"); EnsureUnique(request.DietaryAttributeIds, "Dietary attribute IDs");
        EnsureUnique(request.PreparationMethodIds, "Preparation method IDs"); EnsureUnique(request.TasteProfileIds, "Taste profile IDs");
        EnsureUnique(request.ConfirmedAllergenDeclarations.Select(x => x.AllergenId), "Allergen IDs");
        var ingredients = await _semanticMetadata!.GetActiveIngredientsAsync(request.IngredientIds, ct);
        var allergens = await _semanticMetadata.GetActiveAllergensAsync(request.ConfirmedAllergenDeclarations.Select(x => x.AllergenId).ToArray(), ct);
        var dietary = await _semanticMetadata.GetActiveDietaryAttributesAsync(request.DietaryAttributeIds, ct);
        var preparations = await _semanticMetadata.GetActivePreparationMethodsAsync(request.PreparationMethodIds, ct);
        var tastes = await _semanticMetadata.GetActiveTasteProfilesAsync(request.TasteProfileIds, ct);
        if (ingredients.Count != request.IngredientIds.Count || allergens.Count != request.ConfirmedAllergenDeclarations.Count || dietary.Count != request.DietaryAttributeIds.Count || preparations.Count != request.PreparationMethodIds.Count || tastes.Count != request.TasteProfileIds.Count)
            throw AppException.BadRequest("One or more normalized catalog IDs are invalid or inactive.");
        return new(ingredients, allergens, dietary, preparations, tastes);
    }

    private static void ApplyNormalizedMetadata(FoodItem food, CreateFoodItemV2Request request, MetadataSelection selection, DateTime now)
    {
        food.SpiceLevel = request.SpiceLevel; food.ServingTemperature = request.ServingTemperature;
        food.EstimatedServingCount = request.EstimatedServingCount; food.ServingSizeDescription = request.ServingSizeDescription?.Trim(); food.IsShareable = request.IsShareable;
        Sync(food.Courses, request.AdditionalCourses.Append(request.PrimaryCourse!.Value).ToArray(), x => x.Course,
            course => new() { FoodItemId = food.Id, Course = course, CreatedAt = now });
        foreach (var course in food.Courses) course.IsPrimary = course.Course == request.PrimaryCourse.Value;
        Sync(food.Ingredients, selection.Ingredients.Select(x => x.Id).ToArray(), x => x.IngredientId,
            id => new() { FoodItemId = food.Id, IngredientId = id, Ingredient = selection.Ingredients.Single(x => x.Id == id), CreatedAt = now });
        Sync(food.PreparationMethods, selection.Preparations.Select(x => x.Id).ToArray(), x => x.PreparationMethodId,
            id => new() { FoodItemId = food.Id, PreparationMethodId = id, PreparationMethod = selection.Preparations.Single(x => x.Id == id), CreatedAt = now });
        Sync(food.TasteProfiles, selection.Tastes.Select(x => x.Id).ToArray(), x => x.TasteProfileId,
            id => new() { FoodItemId = food.Id, TasteProfileId = id, TasteProfile = selection.Tastes.Single(x => x.Id == id), CreatedAt = now });

        var requestedAllergens = selection.Allergens.Select(x => x.Id).ToHashSet();
        foreach (var existing in food.Allergens.Where(x => x.Source != MetadataSource.ADMIN_VERIFIED && !requestedAllergens.Contains(x.AllergenId)).ToArray()) food.Allergens.Remove(existing);
        foreach (var item in selection.Allergens)
        {
            var declaration = request.ConfirmedAllergenDeclarations.Single(x => x.AllergenId == item.Id);
            var existing = food.Allergens.SingleOrDefault(x => x.AllergenId == item.Id);
            if (existing is null) food.Allergens.Add(new() { FoodItemId = food.Id, AllergenId = item.Id, Allergen = item, DeclarationType = declaration.DeclarationType, IsConfirmed = false, Source = MetadataSource.OWNER_DECLARED, CreatedAt = now, UpdatedAt = now });
            else if (existing.Source != MetadataSource.ADMIN_VERIFIED) { existing.DeclarationType = declaration.DeclarationType; existing.IsConfirmed = false; existing.Source = MetadataSource.OWNER_DECLARED; existing.UpdatedAt = now; }
        }
        var requestedDietary = selection.Dietary.Select(x => x.Id).ToHashSet();
        foreach (var existing in food.DietaryAttributes.Where(x => x.Source != MetadataSource.ADMIN_VERIFIED && !requestedDietary.Contains(x.DietaryAttributeId)).ToArray()) food.DietaryAttributes.Remove(existing);
        foreach (var item in selection.Dietary)
        {
            var existing = food.DietaryAttributes.SingleOrDefault(x => x.DietaryAttributeId == item.Id);
            if (existing is null) food.DietaryAttributes.Add(new() { FoodItemId = food.Id, DietaryAttributeId = item.Id, DietaryAttribute = item, SuitabilityStatus = DietarySuitabilityStatus.UNVERIFIED, IsConfirmed = false, Source = MetadataSource.OWNER_DECLARED, CreatedAt = now, UpdatedAt = now });
        }
    }

    private static FoodItemV2Response MapV2(FoodItem food) => new()
    {
        Id = food.Id, BoothId = food.BoothId, CategoryId = food.CategoryId, CategoryName = food.Category.Name, Name = food.Name,
        Description = food.Description, Price = food.Price, ThumbnailUrl = food.ThumbnailUrl, IsAvailable = food.IsAvailable, IsFeatured = food.IsFeatured,
        TagIds = food.FoodItemTags.Select(x => x.FoodTagId).ToArray(), CreatedAt = food.CreatedAt, UpdatedAt = food.UpdatedAt,
        SemanticMetadata = new()
        {
            PrimaryCourse = food.Courses.SingleOrDefault(x => x.IsPrimary)?.Course,
            SupportedCourses = food.Courses.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Course).Select(x => x.Course).ToArray(),
            Ingredients = food.Ingredients.OrderBy(x => x.Ingredient.Code).Select(x => new SemanticCatalogResponse(x.IngredientId, x.Ingredient.Code, x.Ingredient.Name)).ToArray(),
            AllergenDeclarations = food.Allergens.OrderBy(x => x.Allergen.Code).Select(x => new FoodAllergenDeclarationResponse(x.AllergenId, x.Allergen.Code, x.Allergen.Name, x.DeclarationType, x.IsConfirmed, x.Source)).ToArray(),
            DietaryAttributes = food.DietaryAttributes.OrderBy(x => x.DietaryAttribute.Code).Select(x => new FoodDietaryAttributeResponse(x.DietaryAttributeId, x.DietaryAttribute.Code, x.DietaryAttribute.Name, x.SuitabilityStatus, x.IsConfirmed, x.Source)).ToArray(),
            PreparationMethods = food.PreparationMethods.OrderBy(x => x.PreparationMethod.Code).Select(x => new SemanticCatalogResponse(x.PreparationMethodId, x.PreparationMethod.Code, x.PreparationMethod.Name)).ToArray(),
            TasteProfiles = food.TasteProfiles.OrderBy(x => x.TasteProfile.Code).Select(x => new SemanticCatalogResponse(x.TasteProfileId, x.TasteProfile.Code, x.TasteProfile.Name)).ToArray(),
            SpiceLevel = food.SpiceLevel, ServingTemperature = food.ServingTemperature, EstimatedServingCount = food.EstimatedServingCount,
            ServingSizeDescription = food.ServingSizeDescription, IsShareable = food.IsShareable
        }
    };

    private sealed record MetadataSelection(IReadOnlyCollection<Ingredient> Ingredients, IReadOnlyCollection<Allergen> Allergens, IReadOnlyCollection<DietaryAttribute> Dietary, IReadOnlyCollection<PreparationMethod> Preparations, IReadOnlyCollection<TasteProfile> Tastes);

    private static void Sync<TItem, TKey>(ICollection<TItem> current, IReadOnlyCollection<TKey> requested, Func<TItem, TKey> key, Func<TKey, TItem> create)
        where TKey : notnull
    {
        var desired = requested.ToHashSet();
        foreach (var item in current.Where(item => !desired.Contains(key(item))).ToArray()) current.Remove(item);
        var existing = current.Select(key).ToHashSet();
        foreach (var value in requested.Where(value => !existing.Contains(value))) current.Add(create(value));
    }
}
