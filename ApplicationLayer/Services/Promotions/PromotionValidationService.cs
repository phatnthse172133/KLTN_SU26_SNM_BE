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
    private readonly TimeProvider _timeProvider;

    public PromotionValidationService(
        IPromotionUsageRepository usages,
        TimeProvider timeProvider)
    {
        _usages = usages;
        _timeProvider = timeProvider;
    }

    public async Task<PromotionValidationResponse> ValidateAsync(Guid customerId, Promotion promotion, IReadOnlyCollection<CartItem> boothItems, CancellationToken cancellationToken = default)
    {
        if (boothItems.Count == 0
            || boothItems.Any(item => item.FoodItem.BoothId != promotion.BoothId))
        {
            throw AppException.BadRequest("Promotion does not belong to this booth.", "PROMOTION_BOOTH_MISMATCH");
        }

        if (boothItems.Any(item => item.Quantity <= 0))
            throw AppException.BadRequest("Cart item quantity is invalid.", "INVALID_CART_ITEM");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now < promotion.StartDate)
            throw AppException.BadRequest("Promotion has not started.", "PROMOTION_NOT_STARTED");

        if (now > promotion.EndDate || promotion.Status == PromotionStatus.Expired)
            throw AppException.BadRequest("Promotion has expired.", "PROMOTION_EXPIRED");

        if (promotion.Status == PromotionStatus.Inactive)
            throw AppException.BadRequest("Promotion is inactive.", "PROMOTION_INACTIVE");

        if (promotion.Status == PromotionStatus.Suspended)
            throw AppException.BadRequest("Promotion is suspended.", "PROMOTION_SUSPENDED");

        if (promotion.Status is not (PromotionStatus.Active or PromotionStatus.Scheduled))
            throw AppException.BadRequest("Promotion is not active.", "PROMOTION_NOT_ACTIVE");

        ValidatePromotionValues(promotion);

        var totalAmount = boothItems.Sum(item => GetLineAmount(item, now));
        if (totalAmount < 0)
            throw AppException.BadRequest(
                "Order subtotal is invalid.",
                "INVALID_ORDER_SUBTOTAL");
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

        var eligibleAmount = GetEligibleAmount(promotion, boothItems, now);
        if (eligibleAmount <= 0)
        {
            throw AppException.BadRequest("The cart has no items eligible for this promotion.", "PROMOTION_NO_ELIGIBLE_ITEMS");
        }

        var calculatedDiscount = promotion.DiscountType switch
        {
            DiscountType.Percentage => eligibleAmount * promotion.DiscountValue / 100m,
            DiscountType.FixedAmount => promotion.DiscountValue,
            _ => throw AppException.BadRequest("Promotion discount type is invalid.", "INVALID_PROMOTION_VALUE")
        };

        var discountAmount = calculatedDiscount;
        if (promotion.DiscountType == DiscountType.Percentage
            && promotion.MaximumDiscountAmount.HasValue)
            discountAmount = Math.Min(discountAmount, promotion.MaximumDiscountAmount.Value);

        // Financial invariants are enforced even if a persisted promotion row
        // was created outside the application validators.
        discountAmount = Math.Min(discountAmount, eligibleAmount);
        discountAmount = Math.Min(discountAmount, totalAmount);
        discountAmount = Math.Max(discountAmount, 0m);
        discountAmount = Math.Round(discountAmount, 2, MidpointRounding.AwayFromZero);
        discountAmount = Math.Min(discountAmount, eligibleAmount);
        discountAmount = Math.Min(discountAmount, totalAmount);
        var finalAmount = totalAmount - discountAmount;

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
            OrderSubtotal = totalAmount,
            EligibleSubtotal = eligibleAmount,
            CalculatedDiscount = calculatedDiscount,
            ActualDiscount = discountAmount,
            FinalAmount = finalAmount
        };
    }

    private static decimal GetEligibleAmount(
        Promotion promotion,
        IReadOnlyCollection<CartItem> boothItems,
        DateTime utcNow)
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
                item => GetLineAmount(item, utcNow)),
            PromotionScope.SpecificFoodItems => boothItems
                .Where(item => foodItemIds.Contains(item.FoodItemId))
                .Sum(item => GetLineAmount(item, utcNow)),
            PromotionScope.SpecificCategories => boothItems
                .Where(item => categoryIds.Contains(item.FoodItem.CategoryId))
                .Sum(item => GetLineAmount(item, utcNow)),
            _ => 0
        };
    }

    private static void ValidatePromotionValues(Promotion promotion)
    {
        var invalidDiscount = promotion.DiscountValue <= 0
            || (promotion.DiscountType == DiscountType.Percentage
                && promotion.DiscountValue > 100)
            || !Enum.IsDefined(promotion.DiscountType)
            || !Enum.IsDefined(promotion.Scope);
        var invalidLimits = promotion.MinimumOrderAmount < 0
            || (promotion.DiscountType == DiscountType.Percentage
                && promotion.MaximumDiscountAmount <= 0)
            || (promotion.DiscountType == DiscountType.FixedAmount
                && promotion.MaximumDiscountAmount.HasValue)
            || promotion.TotalUsageLimit <= 0
            || promotion.UsageLimitPerCustomer <= 0
            || (promotion.TotalUsageLimit.HasValue
                && promotion.UsageLimitPerCustomer > promotion.TotalUsageLimit);

        var invalidDates = promotion.StartDate >= promotion.EndDate;
        var invalidScope = promotion.Scope switch
        {
            PromotionScope.EntireBoothOrder =>
                promotion.PromotionFoodItems.Count > 0
                || promotion.PromotionCategories.Count > 0,
            PromotionScope.SpecificFoodItems =>
                promotion.PromotionFoodItems.Count == 0
                || promotion.PromotionCategories.Count > 0
                || promotion.PromotionFoodItems.Any(target =>
                    target.FoodItem.BoothId != promotion.BoothId),
            PromotionScope.SpecificCategories =>
                promotion.PromotionCategories.Count == 0
                || promotion.PromotionFoodItems.Count > 0
                || promotion.PromotionCategories.Any(target =>
                    target.Category.BoothId != promotion.BoothId),
            _ => true
        };

        if (invalidDiscount || invalidLimits || invalidDates || invalidScope)
            throw AppException.BadRequest("Promotion configuration is invalid.", "INVALID_PROMOTION_VALUE");
    }

    private static decimal GetCurrentPrice(FoodItem foodItem, DateTime utcNow)
        => FoodPriceResolver.GetCurrentPrice(foodItem, utcNow);

    private static decimal GetLineAmount(CartItem item, DateTime utcNow)
    {
        var price = GetCurrentPrice(item.FoodItem, utcNow);
        if (price < 0)
            throw AppException.BadRequest(
                "An eligible item has an invalid current price.",
                "INVALID_ORDER_SUBTOTAL");

        return price * item.Quantity;
    }
}
