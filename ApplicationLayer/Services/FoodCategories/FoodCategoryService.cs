using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.FoodCategories;

public class FoodCategoryService : IFoodCategoryService
{
    private readonly IBoothRepository _booths;
    private readonly IFoodCategoryRepository _categories;
    private readonly IMapper _mapper;

    public FoodCategoryService(IBoothRepository booths, IFoodCategoryRepository categories, IMapper mapper)
    {
        _booths = booths;
        _categories = categories;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PaginationResp<FoodCategoryResponse>>> GetMyBoothCategoriesAsync(Guid ownerId, Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var ownershipError = await ValidateBoothOwnershipAsync(ownerId, boothId);
        if (ownershipError is not null)
            throw ToBoothAccessException(ownershipError);

        var page = await _categories.GetActivePagedByBoothAsync(
            boothId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<FoodCategoryResponse>>.SuccessResponse(
            _mapper.MapPage<FoodCategory, FoodCategoryResponse>(page, pagination));
    }

    public async Task<ApiResponse<FoodCategoryResponse>> GetMyBoothCategoryAsync(Guid ownerId, Guid boothId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        var ownershipError = await ValidateBoothOwnershipAsync(ownerId, boothId);
        if (ownershipError is not null)
            throw ToBoothAccessException(ownershipError);

        var category = await _categories.GetActiveByBoothAsync(boothId, categoryId);

        return category is null
            ? throw AppException.NotFound("Food category was not found.")
            : ApiResponse<FoodCategoryResponse>.SuccessResponse(_mapper.Map<FoodCategoryResponse>(category));
    }

    public async Task<ApiResponse<FoodCategoryResponse>> CreateAsync(Guid ownerId, Guid boothId, CreateFoodCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null)
            throw ToBoothAccessException(managementError);

        var validationError = await ValidateAsync(boothId, request);
        if (validationError is not null)
            throw AppException.BadRequest(validationError);

        var now = DateTime.UtcNow;
        var category = _mapper.Map<FoodCategory>(request);
        category.Id = Guid.NewGuid();
        category.BoothId = boothId;
        category.IsDeleted = false;
        category.CreatedAt = now;
        category.UpdatedAt = now;

        await _categories.AddAsync(category);
        await _categories.SaveChangesAsync();

        return ApiResponse<FoodCategoryResponse>.SuccessResponse(_mapper.Map<FoodCategoryResponse>(category), "Food category created successfully.");
    }

    public async Task<ApiResponse<FoodCategoryResponse>> UpdateAsync(Guid ownerId, Guid boothId, Guid categoryId, UpdateFoodCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null)
            throw ToBoothAccessException(managementError);

        var category = await _categories.GetActiveByBoothAsync(boothId, categoryId);
        if (category is null)
            throw AppException.NotFound("Food category was not found.");

        var validationError = await ValidateAsync(boothId, request, categoryId);
        if (validationError is not null)
            throw AppException.BadRequest(validationError);

        _mapper.Map(request, category);
        category.UpdatedAt = DateTime.UtcNow;

        _categories.Update(category);
        await _categories.SaveChangesAsync();

        return ApiResponse<FoodCategoryResponse>.SuccessResponse(_mapper.Map<FoodCategoryResponse>(category), "Food category updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid ownerId, Guid boothId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        var managementError = await ValidateBoothManagementAsync(ownerId, boothId);
        if (managementError is not null)
            throw ToBoothAccessException(managementError);

        var category = await _categories.GetActiveByBoothAsync(boothId, categoryId);
        if (category is null)
            throw AppException.NotFound("Food category was not found.");

        category.UpdatedAt = DateTime.UtcNow;
        _categories.Delete(category);
        await _categories.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { category.Id }, "Food category deleted successfully.");
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
            return "This booth cannot manage food categories in its current status.";

        return null;
    }

    private async Task<string?> ValidateAsync(Guid boothId, CreateFoodCategoryRequest request, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Food category name is required.";

        var name = request.Name.Trim();
        if (await _categories.ActiveNameExistsAsync(boothId, name, excludeId))
            return "Food category name already exists in this booth.";

        return null;
    }

    private static AppException ToBoothAccessException(string message)
        => message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            ? AppException.Forbidden(message)
            : message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? AppException.NotFound(message)
                : AppException.BadRequest(message);

}
