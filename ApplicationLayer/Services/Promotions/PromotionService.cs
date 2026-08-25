using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using DomainLayer.Common;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.Services.Realtime;

namespace ApplicationLayer.Services.Promotions;

public class PromotionService : IPromotionService
{
    private readonly IBoothRepository _booths;
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionUsageRepository _usages;
    private readonly ICartRepository _carts;
    private readonly ICartItemRepository _cartItems;
    private readonly IFoodItemRepository _foodItems;
    private readonly IFoodCategoryRepository _categories;
    private readonly IPromotionValidationService _validation;
    private readonly IMapper _mapper;
    private readonly ISubscriptionEntitlementService _entitlements;
    private readonly IRealtimeEventPublisher? _realtimeEvents;

    public PromotionService(
        IBoothRepository booths,
        IPromotionRepository promotions,
        IPromotionUsageRepository usages,
        ICartRepository carts,
        ICartItemRepository cartItems,
        IFoodItemRepository foodItems,
        IFoodCategoryRepository categories,
        IPromotionValidationService validation,
        IMapper mapper,
        ISubscriptionEntitlementService entitlements,
        IRealtimeEventPublisher? realtimeEvents = null)
    {
        _booths = booths;
        _promotions = promotions;
        _usages = usages;
        _carts = carts;
        _cartItems = cartItems;
        _foodItems = foodItems;
        _categories = categories;
        _validation = validation;
        _mapper = mapper;
        _entitlements = entitlements;
        _realtimeEvents = realtimeEvents;
    }

    public async Task<ApiResponse<PaginationResp<PromotionResponse>>> GetByBoothAsync(Guid ownerId, Guid boothId, PromotionListRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureBoothAccessAsync(ownerId, boothId, requireManageable: false);
        ValidateListRequest(request);
        var page = await _promotions.GetPagedAsync(
            boothId,
            request.Keyword,
            request.Status,
            request.Scope,
            request.StartDate,
            request.EndDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        return ApiResponse<PaginationResp<PromotionResponse>>.SuccessResponse(
            MapPage(page, request));
    }

    public async Task<ApiResponse<PaginationResp<PromotionResponse>>> GetAllAsync(PromotionListRequest request, CancellationToken cancellationToken = default)
    {
        ValidateListRequest(request);
        var page = await _promotions.GetPagedAsync(
            null,
            request.Keyword,
            request.Status,
            request.Scope,
            request.StartDate,
            request.EndDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        return ApiResponse<PaginationResp<PromotionResponse>>.SuccessResponse(
            MapPage(page, request));
    }

    public async Task<ApiResponse<PromotionResponse>> GetAsync(Guid userId, string role, Guid promotionId, CancellationToken cancellationToken = default)
    {
        var promotion = await GetPromotionAsync(promotionId, cancellationToken);
        EnsurePromotionReadAccess(userId, role, promotion, DateTime.UtcNow);
        return ApiResponse<PromotionResponse>.SuccessResponse(MapPromotion(promotion));
    }

    public async Task<ApiResponse<PromotionResponse>> CreateAsync(Guid ownerId, Guid boothId, CreatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureBoothAccessAsync(ownerId, boothId, requireManageable: true);
        await _entitlements.RequireBoothFeatureAsync(
            boothId,
            entitlement => entitlement.Promotion,
            "Promotions are not included in your current package.",
            "PROMOTION_NOT_INCLUDED");
        var targets = await ValidateRequestAsync(
            boothId,
            request,
            excludePromotionId: null,
            cancellationToken);
        var now = DateTime.UtcNow;
        var promotion = _mapper.Map<Promotion>(request);
        promotion.Id = Guid.NewGuid();
        promotion.BoothId = boothId;
        promotion.StartDate = NormalizeUtc(request.StartDate);
        promotion.EndDate = NormalizeUtc(request.EndDate);
        promotion.Status = promotion.StartDate > now
            ? PromotionStatus.Scheduled
            : PromotionStatus.Active;
        promotion.IsDeleted = false;
        promotion.CreatedAt = now;
        promotion.UpdatedAt = now;

        await _promotions.AddAsync(promotion);
        await _promotions.ReplaceScopeTargetsAsync(
            promotion,
            targets.FoodItemIds,
            targets.CategoryIds,
            cancellationToken);
        await _promotions.SaveChangesAsync();

        promotion = await GetPromotionAsync(promotion.Id, cancellationToken);
        return ApiResponse<PromotionResponse>.SuccessResponse(
            MapPromotion(promotion),
            "Promotion created successfully.");
    }

    public async Task<ApiResponse<PromotionResponse>> UpdateAsync(Guid ownerId, Guid promotionId, UpdatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var promotion = await GetOwnedPromotionAsync(ownerId, promotionId, cancellationToken);
        await _entitlements.RequireBoothFeatureAsync(
            promotion.BoothId,
            entitlement => entitlement.Promotion,
            "Promotions are not included in your current package.",
            "PROMOTION_NOT_INCLUDED");
        await EnsureBoothAccessAsync(ownerId, promotion.BoothId, requireManageable: true);
        var targets = await ValidateRequestAsync(
            promotion.BoothId,
            request,
            promotion.Id,
            cancellationToken);
        EnsureMutableFields(promotion, request, targets);

        _mapper.Map(request, promotion);
        promotion.StartDate = NormalizeUtc(request.StartDate);
        promotion.EndDate = NormalizeUtc(request.EndDate);
        promotion.UpdatedAt = DateTime.UtcNow;

        if (promotion.Status is PromotionStatus.Active or PromotionStatus.Scheduled)
        {
            promotion.Status = promotion.StartDate > promotion.UpdatedAt
                ? PromotionStatus.Scheduled
                : PromotionStatus.Active;
        }

        await _promotions.ReplaceScopeTargetsAsync(
            promotion,
            targets.FoodItemIds,
            targets.CategoryIds,
            cancellationToken);
        await _promotions.SaveChangesAsync();

        promotion = await GetPromotionAsync(promotion.Id, cancellationToken);
        await PublishPromotionChangedAsync(promotion, cancellationToken);
        return ApiResponse<PromotionResponse>.SuccessResponse(
            MapPromotion(promotion),
            "Promotion updated successfully.");
    }

    public async Task<ApiResponse<PromotionResponse>> ActivateAsync(Guid ownerId, Guid promotionId, CancellationToken cancellationToken = default)
    {
        var promotion = await GetOwnedPromotionAsync(ownerId, promotionId, cancellationToken);
        await EnsureBoothAccessAsync(ownerId, promotion.BoothId, requireManageable: true);
        await _entitlements.RequireBoothFeatureAsync(
            promotion.BoothId,
            entitlement => entitlement.Promotion,
            "Promotions are not included in your current package.",
            "PROMOTION_NOT_INCLUDED");
        if (promotion.Status == PromotionStatus.Suspended)
            throw AppException.Forbidden(
                "A banned promotion can only be changed by an administrator.",
                "PROMOTION_ACCESS_DENIED");

        var now = DateTime.UtcNow;
        if (promotion.EndDate < now)
            throw AppException.BadRequest("Promotion has expired.", "PROMOTION_EXPIRED");

        promotion.Status = promotion.StartDate > now
            ? PromotionStatus.Scheduled
            : PromotionStatus.Active;
        promotion.UpdatedAt = now;
        await _promotions.SaveChangesAsync();

        await PublishPromotionChangedAsync(promotion, cancellationToken);
        return ApiResponse<PromotionResponse>.SuccessResponse(
            MapPromotion(promotion),
            "Promotion activated successfully.");
    }

    public async Task<ApiResponse<PromotionResponse>> DeactivateAsync(Guid ownerId, Guid promotionId, CancellationToken cancellationToken = default)
    {
        var promotion = await GetOwnedPromotionAsync(ownerId, promotionId, cancellationToken);
        if (promotion.Status == PromotionStatus.Suspended)
            throw AppException.Forbidden(
                "A banned promotion can only be changed by an administrator.",
                "PROMOTION_ACCESS_DENIED");

        promotion.Status = PromotionStatus.Inactive;
        promotion.UpdatedAt = DateTime.UtcNow;
        await _promotions.SaveChangesAsync();

        await PublishPromotionChangedAsync(promotion, cancellationToken);
        return ApiResponse<PromotionResponse>.SuccessResponse(
            MapPromotion(promotion),
            "Promotion deactivated successfully.");
    }

    public async Task<ApiResponse<PromotionResponse>> SuspendAsync(Guid promotionId, CancellationToken cancellationToken = default)
    {
        var promotion = await GetPromotionAsync(promotionId, cancellationToken);
        promotion.Status = PromotionStatus.Suspended;
        promotion.UpdatedAt = DateTime.UtcNow;
        await _promotions.SaveChangesAsync();

        await PublishPromotionChangedAsync(promotion, cancellationToken);
        return ApiResponse<PromotionResponse>.SuccessResponse(
            MapPromotion(promotion),
            "Promotion banned successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid ownerId, Guid promotionId, CancellationToken cancellationToken = default)
    {
        var promotion = await GetOwnedPromotionAsync(ownerId, promotionId, cancellationToken);
        if (promotion.Status == PromotionStatus.Suspended)
        {
            throw AppException.Forbidden(
                "A banned promotion can only be deleted by an administrator.",
                "PROMOTION_ACCESS_DENIED");
        }

        promotion.UpdatedAt = DateTime.UtcNow;
        _promotions.Delete(promotion);
        await _promotions.SaveChangesAsync();

        await PublishPromotionChangedAsync(promotion, cancellationToken, deleted: true);
        return ApiResponse<object>.SuccessResponse(
            new { promotion.Id },
            "Promotion deleted successfully.");
    }

    public async Task<ApiResponse<PromotionUsageStatisticsResponse>> GetUsageStatisticsAsync(Guid userId,string role, Guid promotionId, CancellationToken cancellationToken = default)
    {
        var promotion = await GetPromotionAsync(promotionId, cancellationToken);
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            && (!role.Equals("BoothOwner", StringComparison.OrdinalIgnoreCase)
                || promotion.Booth.BoothOwnerId != userId))
        {
            throw AppException.Forbidden(
                "You do not have permission to view promotion usage statistics.",
                "PROMOTION_ACCESS_DENIED");
        }

        var statistics = await _usages.GetStatisticsAsync(promotionId, cancellationToken);
        var activeCount = statistics.Reserved + statistics.Consumed;
        return ApiResponse<PromotionUsageStatisticsResponse>.SuccessResponse(new()
        {
            PromotionId = promotionId,
            ReservedCount = statistics.Reserved,
            ConsumedCount = statistics.Consumed,
            ReleasedCount = statistics.Released,
            TotalDiscountAmount = statistics.TotalDiscount,
            TotalUsageLimit = promotion.TotalUsageLimit,
            RemainingUsage = promotion.TotalUsageLimit.HasValue
                ? Math.Max(promotion.TotalUsageLimit.Value - activeCount, 0)
                : null
        });
    }

    public async Task<ApiResponse<PaginationResp<AvailablePromotionResponse>>> GetAvailableForCartAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? throw AppException.NotFound("Cart was not found.", "CART_NOT_FOUND");
        var items = await _cartItems.GetActiveByCartAsync(cart.Id, cancellationToken);
        var boothGroups = items
            .GroupBy(item => item.FoodItem.BoothId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<CartItem>)group.ToList());
        var promotions = await _promotions.GetAvailableAsync(
            boothGroups.Keys.ToList(),
            DateTime.UtcNow,
            cancellationToken);
        var available = new List<AvailablePromotionResponse>();

        foreach (var promotion in promotions)
        {
            if (!boothGroups.TryGetValue(promotion.BoothId, out var boothItems))
                continue;

            try
            {
                var result = await _validation.ValidateAsync(
                    customerId,
                    promotion,
                    boothItems,
                    cancellationToken);
                available.Add(new AvailablePromotionResponse
                {
                    PromotionId = result.PromotionId,
                    BoothId = result.BoothId,
                    BoothName = promotion.Booth.BoothName,
                    PromotionCode = result.PromotionCode,
                    Title = result.Title,
                    DiscountType = result.DiscountType,
                    Scope = result.Scope,
                    TotalAmount = result.TotalAmount,
                    EligibleAmount = result.EligibleAmount,
                    DiscountAmount = result.DiscountAmount,
                    OrderSubtotal = result.OrderSubtotal,
                    EligibleSubtotal = result.EligibleSubtotal,
                    CalculatedDiscount = result.CalculatedDiscount,
                    ActualDiscount = result.ActualDiscount,
                    FinalAmount = result.FinalAmount,
                    DiscountValue = promotion.DiscountValue,
                    MinimumOrderAmount = promotion.MinimumOrderAmount,
                    MaximumDiscountAmount = promotion.MaximumDiscountAmount,
                    StartDate = promotion.StartDate,
                    EndDate = promotion.EndDate
                });
            }
            catch (AppException)
            {
                // Unavailable promotions are omitted from the customer-facing list.
            }
        }

        var pagedItems = available
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToList();
        return ApiResponse<PaginationResp<AvailablePromotionResponse>>.SuccessResponse(
            PaginationResp<AvailablePromotionResponse>.Create(
                pagedItems,
                available.Count,
                pagination));
    }

    public async Task<ApiResponse<PromotionValidationResponse>> ValidateForCartAsync(Guid customerId, ValidateCartPromotionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.BoothId == Guid.Empty)
            throw AppException.BadRequest("Booth id is required.", "BOOTH_ID_REQUIRED");

        if (string.IsNullOrWhiteSpace(request.PromotionCode))
            throw AppException.BadRequest("Promotion code is required.", "PROMOTION_CODE_REQUIRED");

        var cart = await _carts.GetActiveByCustomerAsync(customerId, cancellationToken)
            ?? throw AppException.NotFound("Cart was not found.", "CART_NOT_FOUND");
        var items = await _cartItems.GetActiveByCartAndBoothAsync(
            cart.Id,
            request.BoothId,
            cancellationToken);
        if (items.Count == 0)
        {
            throw AppException.BadRequest(
                "The cart does not contain items from this booth.",
                "PROMOTION_BOOTH_MISMATCH");
        }

        var promotion = await _promotions.GetByCodeAsync(
            request.BoothId,
            request.PromotionCode,
            cancellationToken)
            ?? throw AppException.NotFound(
                "Promotion was not found.",
                "PROMOTION_NOT_FOUND");
        var result = await _validation.ValidateAsync(
            customerId,
            promotion,
            items,
            cancellationToken);
        return ApiResponse<PromotionValidationResponse>.SuccessResponse(
            result,
            "Promotion is valid.");
    }

    private async Task<Promotion> GetPromotionAsync(Guid promotionId, CancellationToken cancellationToken)
        => await _promotions.GetDetailsAsync(promotionId, cancellationToken)
            ?? throw AppException.NotFound(
                "Promotion was not found.",
                "PROMOTION_NOT_FOUND");

    private async Task<Promotion> GetOwnedPromotionAsync(Guid ownerId, Guid promotionId, CancellationToken cancellationToken)
    {
        var promotion = await GetPromotionAsync(promotionId, cancellationToken);
        if (promotion.Booth.BoothOwnerId != ownerId)
        {
            throw AppException.Forbidden(
                "You do not have permission to manage this promotion.",
                "PROMOTION_ACCESS_DENIED");
        }

        return promotion;
    }

    private async Task EnsureBoothAccessAsync(Guid ownerId, Guid boothId,
        bool requireManageable)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        if (booth.BoothOwnerId != ownerId)
        {
            throw AppException.Forbidden(
                "You do not have permission to manage this booth.",
                "BOOTH_OWNERSHIP_REQUIRED");
        }

        if (requireManageable
            && booth.Status is BoothStatus.Banned or BoothStatus.Inactive)
        {
            throw AppException.BadRequest(
                "This booth cannot manage promotions in its current status.",
                "INVALID_BOOTH_STATE");
        }
    }

    private async Task<(IReadOnlyCollection<Guid> FoodItemIds, IReadOnlyCollection<Guid> CategoryIds)> ValidateRequestAsync(Guid boothId,CreatePromotionRequest request,Guid? excludePromotionId,CancellationToken cancellationToken)
    {
        request.StartDate = NormalizeUtc(request.StartDate);
        request.EndDate = NormalizeUtc(request.EndDate);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw AppException.BadRequest("Promotion title is required.", "INVALID_PROMOTION_VALUE");

        if (string.IsNullOrWhiteSpace(request.PromotionCode))
            throw AppException.BadRequest("Promotion code is required.", "PROMOTION_CODE_REQUIRED");

        if (request.StartDate >= request.EndDate || request.EndDate <= DateTime.UtcNow)
            throw AppException.BadRequest("Promotion date range is invalid.", "INVALID_DATE_RANGE");

        if (request.DiscountValue <= 0
            || !Enum.IsDefined(request.DiscountType)
            || !Enum.IsDefined(request.Scope)
            || (request.DiscountType == DiscountType.Percentage
                && request.DiscountValue > 100))
        {
            throw AppException.BadRequest(
                "Promotion discount value is invalid.",
                "INVALID_PROMOTION_VALUE");
        }

        if (request.MinimumOrderAmount < 0
            || request.MaximumDiscountAmount <= 0
            || (request.DiscountType == DiscountType.FixedAmount
                && request.MaximumDiscountAmount.HasValue)
            || request.TotalUsageLimit <= 0
            || request.UsageLimitPerCustomer <= 0
            || (request.TotalUsageLimit.HasValue
                && request.UsageLimitPerCustomer > request.TotalUsageLimit))
        {
            throw AppException.BadRequest(
                "Promotion limits are invalid.",
                "INVALID_PROMOTION_VALUE");
        }

        var code = request.PromotionCode?.Trim();
        if (!string.IsNullOrWhiteSpace(code)
            && await _promotions.CodeExistsAsync(
                boothId,
                code,
                excludePromotionId,
                cancellationToken))
        {
            throw AppException.Conflict(
                "Promotion code already exists in this booth.",
                "PROMOTION_CODE_ALREADY_EXISTS");
        }

        var foodItemIds = request.FoodItemIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var categoryIds = request.CategoryIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        switch (request.Scope)
        {
            case PromotionScope.EntireBoothOrder when foodItemIds.Count > 0 || categoryIds.Count > 0:
                throw AppException.BadRequest(
                    "Entire booth promotions cannot define food or category targets.",
                    "INVALID_PROMOTION_SCOPE");

            case PromotionScope.SpecificFoodItems when foodItemIds.Count == 0 || categoryIds.Count > 0:
                throw AppException.BadRequest(
                    "Food item promotions require food item targets only.",
                    "INVALID_PROMOTION_SCOPE");

            case PromotionScope.SpecificCategories when categoryIds.Count == 0 || foodItemIds.Count > 0:
                throw AppException.BadRequest(
                    "Category promotions require category targets only.",
                    "INVALID_PROMOTION_SCOPE");
        }

        if (foodItemIds.Count > 0)
        {
            var foodItems = await _foodItems.GetActiveByIdsAndBoothAsync(
                boothId,
                foodItemIds,
                cancellationToken);
            if (foodItems.Count != foodItemIds.Count)
            {
                throw AppException.BadRequest(
                    "One or more promotion food items are invalid.",
                    "PROMOTION_FOOD_ITEM_INVALID");
            }
        }

        if (categoryIds.Count > 0)
        {
            var categories = await _categories.GetActiveByIdsAndBoothAsync(
                boothId,
                categoryIds,
                cancellationToken);
            if (categories.Count != categoryIds.Count)
            {
                throw AppException.BadRequest(
                    "One or more promotion categories are invalid.",
                    "PROMOTION_CATEGORY_INVALID");
            }
        }

        return (foodItemIds, categoryIds);
    }

    private static void EnsureMutableFields(Promotion promotion,UpdatePromotionRequest request, (IReadOnlyCollection<Guid> FoodItemIds, IReadOnlyCollection<Guid> CategoryIds) targets)
    {
        var hasUsage = promotion.PromotionUsages.Count > 0;
        if (!hasUsage)
            return;

        var existingFoodIds = promotion.PromotionFoodItems
            .Select(target => target.FoodItemId)
            .ToHashSet();
        var existingCategoryIds = promotion.PromotionCategories
            .Select(target => target.CategoryId)
            .ToHashSet();
        if (promotion.DiscountType != request.DiscountType
            || promotion.DiscountValue != request.DiscountValue
            || promotion.Scope != request.Scope
            || !existingFoodIds.SetEquals(targets.FoodItemIds)
            || !existingCategoryIds.SetEquals(targets.CategoryIds))
        {
            throw AppException.Conflict(
                "Discount type, value, scope and targets cannot change after promotion usage exists.",
                "PROMOTION_ALREADY_USED");
        }

        var activeUsageCount = promotion.PromotionUsages.Count(
            usage => usage.Status != PromotionUsageStatus.Released);
        if (request.TotalUsageLimit.HasValue
            && request.TotalUsageLimit.Value < activeUsageCount)
        {
            throw AppException.Conflict(
                "Total usage limit cannot be lower than current usage.",
                "PROMOTION_USAGE_LIMIT_INVALID");
        }
    }

    private static void EnsurePromotionReadAccess(
        Guid userId,
        string role,
        Promotion promotion,
        DateTime utcNow)
    {
        var isCustomerVisible = promotion.IsPublic
            && !string.IsNullOrWhiteSpace(promotion.PromotionCode)
            && promotion.StartDate <= utcNow
            && promotion.EndDate >= utcNow
            && promotion.Status is PromotionStatus.Active or PromotionStatus.Scheduled;
        var allowed = role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || (role.Equals("BoothOwner", StringComparison.OrdinalIgnoreCase)
                && promotion.Booth.BoothOwnerId == userId)
            || (role.Equals("Customer", StringComparison.OrdinalIgnoreCase)
                && isCustomerVisible);
        if (!allowed)
        {
            throw AppException.Forbidden(
                "You do not have permission to view this promotion.",
                "PROMOTION_ACCESS_DENIED");
        }
    }

    private PaginationResp<PromotionResponse> MapPage(PagedResult<Promotion> page, PaginationReq request)
        => PaginationResp<PromotionResponse>.Create(
            page.Items.Select(MapPromotion).ToList(),
            page.TotalCount,
            request);

    private PromotionResponse MapPromotion(Promotion promotion)
    {
        var response = _mapper.Map<PromotionResponse>(promotion);
        if (promotion.DiscountType == DiscountType.FixedAmount
            && promotion.DiscountValue > (promotion.MinimumOrderAmount ?? 0m))
        {
            response.ConfigurationWarnings =
                ["FIXED_DISCOUNT_CAN_CREATE_FREE_ORDER"];
        }

        if (promotion.Status is PromotionStatus.Active or PromotionStatus.Scheduled)
        {
            var now = DateTime.UtcNow;
            response.Status = now < promotion.StartDate
                ? PromotionStatus.Scheduled.ToString()
                : now > promotion.EndDate
                    ? PromotionStatus.Expired.ToString()
                    : PromotionStatus.Active.ToString();
        }

        return response;
    }

    private static void ValidateListRequest(PromotionListRequest request)
    {
        if (request.StartDate.HasValue)
            request.StartDate = NormalizeUtc(request.StartDate.Value);

        if (request.EndDate.HasValue)
            request.EndDate = NormalizeUtc(request.EndDate.Value);

        if (request.StartDate.HasValue
            && request.EndDate.HasValue
            && request.StartDate.Value > request.EndDate.Value)
        {
            throw AppException.BadRequest(
                "Promotion filter date range is invalid.",
                "INVALID_DATE_RANGE");
        }
    }

    private async Task PublishPromotionChangedAsync(
        Promotion promotion,
        CancellationToken cancellationToken,
        bool deleted = false)
    {
        if (_realtimeEvents is null) return;
        try
        {
            await _realtimeEvents.PublishAsync(new RealtimeEvent
            {
                EventType = "PromotionChanged",
                GroupName = RealtimeGroups.Booth(promotion.BoothId),
                Payload = new
                {
                    promotionId = promotion.Id,
                    boothId = promotion.BoothId,
                    status = deleted ? "Deleted" : promotion.Status.ToString(),
                    deleted
                }
            }, cancellationToken);
        }
        catch
        {
            // Non-fatal scoped fan-out to booth listeners only.
        }
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
