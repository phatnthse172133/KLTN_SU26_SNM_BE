using ApplicationLayer.Exceptions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Helppers;

public static class PackagePriceValidator
{
    public static async Task ValidatePackagePriceAsync(
        IPackagePriceRepository packagePrices,
        Package package,
        decimal price,
        int duration,
        DateTime? startDate,
        DateTime? endDate,
        Guid? currentPriceId = null)
    {
        var isFree = package.Code == "BOOTH_FREE" || package.Price == 0;

        if (isFree)
            throw AppException.BadRequest("Free packages cannot have additional prices or promotions.", "PROMOTION_NOT_ALLOWED_FOR_FREE_PACKAGE");

        if (duration <= 0)
            throw AppException.BadRequest("Duration days must be greater than zero.");

        if (price <= 0)
            throw AppException.BadRequest("Price must be greater than zero.");

        // Regular Price
        if (!startDate.HasValue && !endDate.HasValue)
        {
            var existingRegular = await packagePrices.FirstOrDefaultAsync(p =>
                p.PackageId == package.Id &&
                p.DurationDays == duration &&
                !p.StartDate.HasValue &&
                !p.EndDate.HasValue &&
                (!currentPriceId.HasValue || p.Id != currentPriceId.Value));

            if (existingRegular != null)
                throw AppException.BadRequest($"A regular price for duration {duration} days already exists.");

            return;
        }

        // Only one date provided
        if (!startDate.HasValue || !endDate.HasValue)
            throw AppException.BadRequest("Both start date and end date are required for a promotion.", "PROMOTION_DATES_REQUIRED");

        // Promotion Price
        if (startDate.Value >= endDate.Value)
            throw AppException.BadRequest("Start date must be earlier than end date.", "INVALID_PROMOTION_DATE");

        if (endDate.Value <= DateTime.UtcNow)
            throw AppException.BadRequest("Promotion end date must be in the future.", "PROMOTION_ALREADY_EXPIRED");

        // Find the base price for this duration
        decimal basePrice;
        if (duration == package.DurationDays)
        {
            basePrice = package.Price;
        }
        else
        {
            var regularBase = await packagePrices.FirstOrDefaultAsync(p =>
                p.PackageId == package.Id &&
                p.DurationDays == duration &&
                !p.StartDate.HasValue &&
                !p.EndDate.HasValue &&
                (!currentPriceId.HasValue || p.Id != currentPriceId.Value));

            if (regularBase == null)
                throw AppException.BadRequest($"Cannot create a promotion for {duration} days because no regular base price exists for this duration.");

            basePrice = regularBase.Price;
        }

        if (price >= basePrice)
            throw AppException.BadRequest("Promotion price must be strictly less than the base price for this duration.", "INVALID_PROMOTION_PRICE");

        // Check for promotion overlap for the same duration
        var overlapping = await packagePrices.FirstOrDefaultAsync(p =>
            p.PackageId == package.Id &&
            p.DurationDays == duration &&
            p.StartDate.HasValue && p.EndDate.HasValue &&
            p.StartDate.Value <= endDate.Value && p.EndDate.Value >= startDate.Value &&
            (!currentPriceId.HasValue || p.Id != currentPriceId.Value));

        if (overlapping != null)
            throw AppException.BadRequest($"This promotion overlaps with an existing promotion for {duration} days.");
    }
}
