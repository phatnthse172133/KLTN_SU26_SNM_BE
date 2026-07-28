using ApplicationLayer.AI.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.AI.Services;

public class FoodTagService : IFoodTagService
{
    private readonly IFoodTagRepository _foodTags;
    private readonly IFoodItemRepository _foodItems;

    public FoodTagService(IFoodTagRepository foodTags, IFoodItemRepository foodItems)
    {
        _foodTags = foodTags;
        _foodItems = foodItems;
    }

    public async Task<ApiResponse<PaginationResp<FoodTagResponse>>> GetAsync(
        FoodTagQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await _foodTags.GetPagedTagsAsync(
            request.Search,
            request.TagGroup,
            request.Page,
            request.PageSize,
            cancellationToken);

        var response = PaginationResp<FoodTagResponse>.Create(
            page.Items.Select(Map).ToList(),
            page.TotalCount,
            request);

        return ApiResponse<PaginationResp<FoodTagResponse>>.SuccessResponse(response);
    }

    public async Task<ApiResponse<FoodTagResponse>> CreateAsync(
        CreateFoodTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        if (await _foodTags.AnyAsync(tag => tag.Code == code && !tag.IsDeleted))
            throw AppException.Conflict("Food tag code already exists.");

        var now = DateTime.UtcNow;
        var entity = new FoodTag
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = code,
            Description = TextHelper.NormalizeOptionalText(request.Description),
            TagGroup = request.TagGroup,
            Status = FoodTagStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _foodTags.AddAsync(entity);
        await _foodTags.SaveChangesAsync();

        return ApiResponse<FoodTagResponse>.SuccessResponse(Map(entity), "Food tag created successfully.");
    }

    public async Task<ApiResponse<FoodTagResponse>> UpdateAsync(
        Guid tagId,
        UpdateFoodTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var tag = await _foodTags.GetByIdAsync(tagId)
            ?? throw AppException.NotFound("Food tag was not found.");
        if (tag.IsSystem)
            throw AppException.Forbidden("System food tags can only be changed by a versioned backend seed.");
        var code = NormalizeCode(request.Code);

        if (await _foodTags.AnyAsync(existing => existing.Id != tagId && existing.Code == code && !existing.IsDeleted))
            throw AppException.Conflict("Food tag code already exists.");

        tag.Name = request.Name.Trim();
        tag.Code = code;
        tag.Description = TextHelper.NormalizeOptionalText(request.Description);
        tag.TagGroup = request.TagGroup;
        tag.Status = request.Status;
        tag.UpdatedAt = DateTime.UtcNow;

        _foodTags.Update(tag);
        await _foodTags.SaveChangesAsync();

        return ApiResponse<FoodTagResponse>.SuccessResponse(Map(tag), "Food tag updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        var tag = await _foodTags.GetByIdAsync(tagId)
            ?? throw AppException.NotFound("Food tag was not found.");
        if (tag.IsSystem)
            throw AppException.Forbidden("System food tags cannot be deleted.");

        tag.Status = FoodTagStatus.Inactive;
        tag.UpdatedAt = DateTime.UtcNow;
        _foodTags.Delete(tag);
        await _foodTags.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { tag.Id }, "Food tag deleted successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<FoodTagResponse>>> UpdateFoodItemTagsAsync(
        Guid ownerId,
        Guid foodItemId,
        UpdateFoodItemTagsRequest request,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var foodItem = await _foodItems.GetWithTagsAsync(foodItemId, cancellationToken)
            ?? throw AppException.NotFound("Food item was not found.");

        if (!isAdmin && foodItem.Booth.BoothOwnerId != ownerId)
            throw AppException.Forbidden("You do not have permission to update this food item.");

        if (request.TagIds.Count != request.TagIds.Distinct().Count())
            throw AppException.BadRequest("Food tag IDs must not contain duplicates.");
        var uniqueTagIds = request.TagIds.ToList();
        var tags = await _foodTags.GetActiveByIdsAsync(uniqueTagIds, cancellationToken);
        if (tags.Count != uniqueTagIds.Count)
            throw AppException.BadRequest("One or more food tags are invalid.");
        FoodTagSelectionPolicy.Validate(tags);

        var requestedIds = uniqueTagIds.ToHashSet();
        foreach (var existing in foodItem.FoodItemTags.Where(item => !requestedIds.Contains(item.FoodTagId)).ToList())
            foodItem.FoodItemTags.Remove(existing);

        var existingIds = foodItem.FoodItemTags.Select(item => item.FoodTagId).ToHashSet();
        foreach (var tagId in uniqueTagIds.Where(tagId => !existingIds.Contains(tagId)))
        {
            foodItem.FoodItemTags.Add(new FoodItemTag
            {
                FoodItemId = foodItemId,
                FoodTagId = tagId,
                CreatedAt = DateTime.UtcNow
            });
        }

        foodItem.UpdatedAt = DateTime.UtcNow;
        _foodItems.Update(foodItem);
        await _foodItems.SaveChangesAsync();

        return ApiResponse<IReadOnlyCollection<FoodTagResponse>>.SuccessResponse(
            tags.Select(Map).ToList(),
            "Food item tags updated successfully.");
    }

    private static FoodTagResponse Map(FoodTag tag)
        => new()
        {
            Id = tag.Id,
            Name = tag.Name,
            Code = tag.Code,
            Description = tag.Description,
            TagGroup = tag.TagGroup.ToString(),
            Status = tag.Status.ToString(),
            IsSystem = tag.IsSystem,
            DisplayOrder = tag.DisplayOrder,
            IsSelectable = tag.IsSelectable,
            IsPreferenceSelectable = tag.IsPreferenceSelectable,
            IsAutoAssigned = tag.IsAutoAssigned
        };

    private static string NormalizeCode(string code)
        => code.Trim().ToUpperInvariant().Replace(" ", "_");
}
