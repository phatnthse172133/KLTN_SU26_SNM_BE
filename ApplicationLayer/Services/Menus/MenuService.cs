using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
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
    private readonly IMapper _mapper;

    public MenuService(
        IBoothRepository booths,
        IFoodCategoryRepository categories,
        IFoodItemRepository foodItems,
        IMapper mapper)
    {
        _booths = booths;
        _categories = categories;
        _foodItems = foodItems;
        _mapper = mapper;
    }

    public async Task<ApiResponse<IReadOnlyCollection<FoodItemResponse>>> GetMyBoothMenuAsync(Guid ownerId, Guid boothId, CancellationToken cancellationToken = default)
    {
        var ownershipError = await ValidateBoothOwnershipAsync(ownerId, boothId);
        if (ownershipError is not null) 
            throw ToBoothAccessException(ownershipError);

        var items = await _foodItems.GetMenuByBoothAsync(boothId);
        return ApiResponse<IReadOnlyCollection<FoodItemResponse>>.SuccessResponse(_mapper.Map<List<FoodItemResponse>>(items));
    }

    public async Task<ApiResponse<FoodItemResponse>> CreateFoodItemAsync(Guid ownerId, Guid boothId, CreateFoodItemRequest request, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null) 
            throw ToBoothAccessException(managementError);

        var (validationError, category) = await ValidateFoodItemRequestAsync(boothId, request);
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

        var (validationError, category) = await ValidateFoodItemRequestAsync(boothId, request);
        if (validationError is not null)
            throw validationError.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? AppException.NotFound(validationError)
                : AppException.BadRequest(validationError);

        _mapper.Map(request, foodItem);
        foodItem.Category = category!;
        foodItem.UpdatedAt = DateTime.UtcNow;

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

        if (booth.Status is BoothStatus.Suspended or BoothStatus.Closed) 
            return "This booth cannot manage menu items in its current status.";

        return null;
    }

    private async Task<(string? Error, FoodCategory? Category)> ValidateFoodItemRequestAsync(Guid boothId, CreateFoodItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) 
            return ("Food item name is required.", null);

        if (request.Price <= 0) 
            return ("Food item price must be greater than zero.", null);

        var category = await _categories.GetActiveByBoothAsync(boothId, request.CategoryId);
        if (category is null)
            return ("Food category was not found.", null);

        return (null, category);
    }

    private static AppException ToBoothAccessException(string message)
        => message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            ? AppException.Forbidden(message)
            : message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? AppException.NotFound(message)
                : AppException.BadRequest(message);
}
