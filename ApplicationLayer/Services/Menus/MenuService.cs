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

namespace ApplicationLayer.Services.Menus;

public class MenuService : IMenuService
{
    private readonly IBoothRepository _booths;
    private readonly IFoodCategoryRepository _categories;
    private readonly IFoodItemRepository _foodItems;
    private readonly IFoodTagRepository _foodTags;
    private readonly IMapper _mapper;

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

        await _foodItems.AddAsync(foodItem);
        await _foodItems.SaveChangesAsync();

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

        _foodItems.Update(foodItem);
        await _foodItems.SaveChangesAsync();

        return ApiResponse<FoodItemResponse>.SuccessResponse(_mapper.Map<FoodItemResponse>(foodItem), "Food item updated successfully.");
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
}
