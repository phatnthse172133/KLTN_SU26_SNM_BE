using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using DomainLayer.Entities;
using DomainLayer.Common;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Promotions;

public class PromotionValidationService : IPromotionValidationService
{
    private readonly IPromotionUsageRepository _usages;

    public PromotionValidationService(IPromotionUsageRepository usages)
    {
        _usages = usages;
    }

    public async Task<PromotionValidationResponse> ValidateAsync(Guid customerId, Promotion promotion, IReadOnlyCollection<CartItem> boothItems, CancellationToken cancellationToken = default)
    {
        if (boothItems.Count == 0 || boothItems.Any(item => item.FoodItem.BoothId != promotion.BoothId))
        {
            throw AppException.BadRequest("Promotion does not belong to this booth.", "PROMOTION_BOOTH_MISMATCH");
        }

        var now = DateTime.UtcNow;
        if (now < promotion.StartDate)
            throw AppException.BadRequest("Promotion has not started.", "PROMOTION_NOT_STARTED");

        if (now > promotion.EndDate || promotion.Status == PromotionStatus.Expired)
            throw AppException.BadRequest("Promotion has expired.", "PROMOTION_EXPIRED");

        if (promotion.Status == PromotionStatus.Inactive)
            throw AppException.BadRequest("Promotion is inactive.", "PROMOTION_INACTIVE");

        if (promotion.Status == PromotionStatus.Suspended)
            throw AppException.BadRequest("Promotion is suspended.", "PROMOTION_SUSPENDED");

        var totalAmount = boothItems.Sum(item => GetCurrentPrice(item.FoodItem) * item.Quantity);
        if (promotion.MinimumOrderAmount.HasValue
            && totalAmount < promotion.MinimumOrderAmount.Value)
        {
            throw AppException.BadRequest("Minimum order amount has not been met.", "PROMOTION_MINIMUM_ORDER_NOT_MET");
        }

        var activeUsageCount = await _usages.CountActiveAsync(promotion.Id, cancellationToken);
        if (promotion.TotalUsageLimit.HasValue
            && activeUsageCount >= promotion.TotalUsageLimit.Value)
        {
            throw AppException.Conflict("Promotion usage limit has been reached.", "PROMOTION_USAGE_LIMIT_REACHED");
        }

        var customerUsageCount = await _usages.CountActiveByCustomerAsync(promotion.Id, customerId, cancellationToken);
        if (promotion.UsageLimitPerCustomer.HasValue
            && customerUsageCount >= promotion.UsageLimitPerCustomer.Value)
        {
            throw AppException.Conflict("Customer promotion usage limit has been reached.", "PROMOTION_CUSTOMER_LIMIT_REACHED");
        }

        var eligibleAmount = GetEligibleAmount(promotion, boothItems);
        if (eligibleAmount <= 0)
        {
            throw AppException.BadRequest("The cart has no items eligible for this promotion.", "PROMOTION_NO_ELIGIBLE_ITEMS");
        }

        var discountAmount = promotion.DiscountType switch
        {
            DiscountType.Percentage => eligibleAmount * promotion.DiscountValue / 100m,
            DiscountType.FixedAmount => Math.Min(promotion.DiscountValue, eligibleAmount),
            _ => throw AppException.BadRequest("Promotion discount type is invalid.", "INVALID_PROMOTION_VALUE")
        };

        if (promotion.MaximumDiscountAmount.HasValue)
            discountAmount = Math.Min(discountAmount, promotion.MaximumDiscountAmount.Value);

        discountAmount = Math.Round(discountAmount, 2, MidpointRounding.AwayFromZero);

        return new PromotionValidationResponse
        {
            PromotionId = promotion.Id,
            BoothId = promotion.BoothId,
            PromotionCode = promotion.PromotionCode,
            Title = promotion.Title,
            DiscountType = promotion.DiscountType.ToString(),
            Scope = promotion.Scope.ToString(),
            TotalAmount = totalAmount,
            EligibleAmount = eligibleAmount,
            DiscountAmount = discountAmount,
            FinalAmount = Math.Max(totalAmount - discountAmount, 0)
        };
    }

    private static decimal GetEligibleAmount(Promotion promotion, IReadOnlyCollection<CartItem> boothItems)
    {
        var foodItemIds = promotion.PromotionFoodItems
            .Select(target => target.FoodItemId)
            .ToHashSet();
        var categoryIds = promotion.PromotionCategories
            .Select(target => target.CategoryId)
            .ToHashSet();

        return promotion.Scope switch
        {
            PromotionScope.EntireBoothOrder => boothItems.Sum(
                item => GetCurrentPrice(item.FoodItem) * item.Quantity),
            PromotionScope.SpecificFoodItems => boothItems
                .Where(item => foodItemIds.Contains(item.FoodItemId))
                .Sum(item => GetCurrentPrice(item.FoodItem) * item.Quantity),
            PromotionScope.SpecificCategories => boothItems
                .Where(item => categoryIds.Contains(item.FoodItem.CategoryId))
                .Sum(item => GetCurrentPrice(item.FoodItem) * item.Quantity),
            _ => 0
        };
    }

    private static decimal GetCurrentPrice(FoodItem foodItem)
        => FoodPriceResolver.GetCurrentPrice(foodItem, DateTime.UtcNow);
}
