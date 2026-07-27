using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using System.Linq;

namespace ApplicationLayer.Services.Prices;

public class PriceService : IPriceService
{
    private readonly IBoothRepository _booths;
    private readonly IFoodItemRepository _foodItems;
    private readonly IFoodPriceRepository _foodPrices;
    private readonly IGenericRepository<Package> _packages;
    private readonly IPackagePriceRepository _packagePrices;
    private readonly IMapper _mapper;

    public PriceService(
        IBoothRepository booths,
        IFoodItemRepository foodItems,
        IFoodPriceRepository foodPrices,
        IGenericRepository<Package> packages,
        IPackagePriceRepository packagePrices,
        IMapper mapper)
    {
        _booths = booths;
        _foodItems = foodItems;
        _foodPrices = foodPrices;
        _packages = packages;
        _packagePrices = packagePrices;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PaginationResp<FoodPriceResponse>>> GetFoodPricesAsync(
        Guid ownerId,
        Guid boothId,
        Guid foodItemId,
        PaginationReq pagination,
        CancellationToken cancellationToken = default)
    {
        await EnsureFoodItemAccessAsync(ownerId, boothId, foodItemId, requireManageableBooth: false);
        var page = await _foodPrices.GetByFoodItemPagedAsync(
            foodItemId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<FoodPriceResponse>>.SuccessResponse(
            _mapper.MapPage<FoodPrice, FoodPriceResponse>(page, pagination));
    }

    public async Task<ApiResponse<FoodPriceResponse>> CreateFoodPriceAsync(Guid ownerId, Guid boothId, Guid foodItemId, CreatePriceRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureFoodItemAccessAsync(ownerId, boothId, foodItemId, requireManageableBooth: true);
        ValidatePriceRequest(request);

        var now = DateTime.UtcNow;
        var foodPrice = _mapper.Map<FoodPrice>(request);
        foodPrice.Id = Guid.NewGuid();
        foodPrice.FoodItemId = foodItemId;
        foodPrice.CreatedAt = now;
        foodPrice.UpdatedAt = now;

        await _foodPrices.AddAsync(foodPrice);
        await _foodPrices.SaveChangesAsync();

        return ApiResponse<FoodPriceResponse>.SuccessResponse(_mapper.Map<FoodPriceResponse>(foodPrice), "Food price created successfully.");
    }

    public async Task<ApiResponse<FoodPriceResponse>> UpdateFoodPriceAsync(Guid ownerId, Guid boothId, Guid foodItemId, Guid priceId, UpdatePriceRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureFoodItemAccessAsync(ownerId, boothId, foodItemId, requireManageableBooth: true);
        ValidatePriceRequest(request);

        var foodPrice = await _foodPrices.FirstOrDefaultAsync(price => price.Id == priceId && price.FoodItemId == foodItemId);
        if (foodPrice is null)
            throw AppException.NotFound("Food price was not found.");

        _mapper.Map(request, foodPrice);
        foodPrice.UpdatedAt = DateTime.UtcNow;

        _foodPrices.Update(foodPrice);
        await _foodPrices.SaveChangesAsync();

        return ApiResponse<FoodPriceResponse>.SuccessResponse(_mapper.Map<FoodPriceResponse>(foodPrice), "Food price updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteFoodPriceAsync(Guid ownerId, Guid boothId, Guid foodItemId, Guid priceId, CancellationToken cancellationToken = default)
    {
        await EnsureFoodItemAccessAsync(ownerId, boothId, foodItemId, requireManageableBooth: true);

        var foodPrice = await _foodPrices.FirstOrDefaultAsync(price => price.Id == priceId && price.FoodItemId == foodItemId);
        if (foodPrice is null)
            throw AppException.NotFound("Food price was not found.");

        foodPrice.UpdatedAt = DateTime.UtcNow;
        _foodPrices.Delete(foodPrice);
        await _foodPrices.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { foodPrice.Id }, "Food price deleted successfully.");
    }

    public async Task<ApiResponse<PaginationResp<PackagePriceResponse>>> GetPackagePricesAsync(
        Guid packageId,
        PaginationReq pagination,
        CancellationToken cancellationToken = default)
    {
        await EnsureAndGetPackageAsync(packageId);
        var page = await _packagePrices.GetByPackagePagedAsync(
            packageId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<PackagePriceResponse>>.SuccessResponse(
            _mapper.MapPage<PackagePrice, PackagePriceResponse>(page, pagination));
    }

    public async Task<ApiResponse<List<PublicPackagePriceResponse>>> GetPublicPackagePricesAsync(
        Guid packageId,
        CancellationToken cancellationToken = default)
    {
        var package = await _packages.FirstOrDefaultAsync(p => p.Id == packageId && !p.IsDeleted && p.Status == PackageStatus.Active);
        if (package is null)
            throw AppException.NotFound("Package was not found or is not active.");

        var now = DateTime.UtcNow;
        var allPrices = (await _packagePrices.FindAsync(
            p => p.PackageId == packageId && !p.IsDeleted)).ToList();

        var durations = allPrices.Select(p => p.DurationDays).Distinct().ToList();
        if (!durations.Contains(package.DurationDays) && package.DurationDays > 0)
            durations.Add(package.DurationDays);

        var result = new List<PublicPackagePriceResponse>();

        foreach (var duration in durations.OrderBy(d => d))
        {
            decimal basePrice;
            if (duration == package.DurationDays)
            {
                basePrice = package.Price;
            }
            else
            {
                var alternativeBase = allPrices.FirstOrDefault(p => p.DurationDays == duration && !p.StartDate.HasValue && !p.EndDate.HasValue);
                if (alternativeBase == null) continue;
                basePrice = alternativeBase.Price;
            }

            var activePromotion = allPrices
                .Where(p => p.DurationDays == duration && p.StartDate.HasValue && p.EndDate.HasValue)
                .Where(p => p.StartDate <= now && p.EndDate >= now)
                .OrderBy(p => p.Price)
                .FirstOrDefault();

            result.Add(new PublicPackagePriceResponse
            {
                DurationDays = duration,
                BasePrice = basePrice,
                EffectivePrice = activePromotion != null ? activePromotion.Price : basePrice,
                HasPromotion = activePromotion != null,
                PromotionEndDate = activePromotion?.EndDate
            });
        }

        return ApiResponse<List<PublicPackagePriceResponse>>.SuccessResponse(result);
    }

    public async Task<ApiResponse<PackagePriceResponse>> CreatePackagePriceAsync(Guid packageId, CreatePriceRequest request, CancellationToken cancellationToken = default)
    {
        var package = await EnsureAndGetPackageAsync(packageId);

        var duration = request.DurationDays ?? package.DurationDays;
        var normalizedStart = NormalizeUtc(request.StartDate);
        var normalizedEnd = NormalizeUtc(request.EndDate);
        await PackagePriceValidator.ValidatePackagePriceAsync(_packagePrices, package, request.Price, duration, normalizedStart, normalizedEnd);

        var now = DateTime.UtcNow;
        var packagePrice = _mapper.Map<PackagePrice>(request);
        packagePrice.Id = Guid.NewGuid();
        packagePrice.PackageId = packageId;
        packagePrice.DurationDays = duration;
        packagePrice.StartDate = normalizedStart;
        packagePrice.EndDate = normalizedEnd;
        packagePrice.CreatedAt = now;
        packagePrice.UpdatedAt = now;

        await _packagePrices.AddAsync(packagePrice);
        await _packagePrices.SaveChangesAsync();

        return ApiResponse<PackagePriceResponse>.SuccessResponse(_mapper.Map<PackagePriceResponse>(packagePrice), "Package price created successfully.");
    }

    public async Task<ApiResponse<PackagePriceResponse>> UpdatePackagePriceAsync(Guid packageId, Guid priceId, UpdatePriceRequest request, CancellationToken cancellationToken = default)
    {
        var package = await EnsureAndGetPackageAsync(packageId);

        var duration = request.DurationDays ?? package.DurationDays;
        var normalizedStart = NormalizeUtc(request.StartDate);
        var normalizedEnd = NormalizeUtc(request.EndDate);
        await PackagePriceValidator.ValidatePackagePriceAsync(_packagePrices, package, request.Price, duration, normalizedStart, normalizedEnd, priceId);

        var packagePrice = await _packagePrices.FirstOrDefaultAsync(price => price.Id == priceId && price.PackageId == packageId);
        if (packagePrice is null)
            throw AppException.NotFound("Package price was not found.");

        _mapper.Map(request, packagePrice);
        packagePrice.DurationDays = duration;
        packagePrice.StartDate = normalizedStart;
        packagePrice.EndDate = normalizedEnd;
        packagePrice.UpdatedAt = DateTime.UtcNow;

        _packagePrices.Update(packagePrice);
        await _packagePrices.SaveChangesAsync();

        return ApiResponse<PackagePriceResponse>.SuccessResponse(_mapper.Map<PackagePriceResponse>(packagePrice), "Package price updated successfully.");
    }

    public async Task<ApiResponse<object>> DeletePackagePriceAsync(Guid packageId, Guid priceId, CancellationToken cancellationToken = default)
    {
        await EnsureAndGetPackageAsync(packageId);

        var packagePrice = await _packagePrices.FirstOrDefaultAsync(price => price.Id == priceId && price.PackageId == packageId);
        if (packagePrice is null)
            throw AppException.NotFound("Package price was not found.");

        packagePrice.UpdatedAt = DateTime.UtcNow;
        _packagePrices.Delete(packagePrice);
        await _packagePrices.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { packagePrice.Id }, "Package price deleted successfully.");
    }

    private async Task EnsureFoodItemAccessAsync(Guid ownerId, Guid boothId, Guid foodItemId, bool requireManageableBooth)
    {
        var booth = requireManageableBooth
            ? await _booths.GetOwnedBoothAsync(ownerId, boothId)
            : await _booths.GetByIdAsync(boothId);

        if (booth is null)
        {
            throw await _booths.AnyAsync(booth => booth.Id == boothId)
                ? AppException.Forbidden("You do not have permission to manage this booth.")
                : AppException.NotFound("Booth was not found.");
        }

        if (booth.BoothOwnerId != ownerId)
            throw AppException.Forbidden("You do not have permission to manage this booth.");

        if (requireManageableBooth && booth.Status is BoothStatus.Banned)
            throw AppException.BadRequest("This booth cannot manage prices in its current status.");

        var foodItem = await _foodItems.GetByBoothAsync(boothId, foodItemId);
        if (foodItem is null)
            throw AppException.NotFound("Food item was not found.");
    }

    private async Task<Package> EnsureAndGetPackageAsync(Guid packageId)
    {
        var package = await _packages.FirstOrDefaultAsync(package => package.Id == packageId);
        if (package is null)
            throw AppException.NotFound("Package was not found.");
        return package;
    }



    private static void ValidatePriceRequest(CreatePriceRequest request)
    {
        if (request.Price <= 0)
            throw AppException.BadRequest("Price must be greater than zero.");

        if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate.Value >= request.EndDate.Value)
            throw AppException.BadRequest("Start date must be earlier than end date.", "INVALID_DATE_RANGE");
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue) return null;
        return value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
    }
}
