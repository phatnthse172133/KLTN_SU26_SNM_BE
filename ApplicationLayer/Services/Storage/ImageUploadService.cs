using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.Storage;

public interface IImageUploadService
{
    Task<ApiResponse<object>> UploadMarketThumbnailAsync(Guid marketOwnerId, Guid marketId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default);
    Task<ApiResponse<object>> UploadBoothThumbnailByMarketOwnerAsync(Guid marketOwnerId, Guid boothId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default);
    Task<ApiResponse<object>> UploadBoothThumbnailByBoothOwnerAsync(Guid boothOwnerId, Guid boothId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default);
    Task<ApiResponse<object>> UploadFoodItemThumbnailAsync(Guid boothOwnerId, Guid foodItemId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default);
    Task<ApiResponse<object>> UploadLayoutImageAsync(Guid marketOwnerId, Guid layoutId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default);
}

public class ImageUploadService : IImageUploadService
{
    private readonly IFileStorageService _fileStorage;
    private readonly INightMarketRepository _nightMarkets;
    private readonly IBoothRepository _booths;
    private readonly IFoodItemRepository _foodItems;
    private readonly IMarketLayoutRepository _layouts;
    private readonly ILogger<ImageUploadService> _logger;

    public ImageUploadService(
        IFileStorageService fileStorage,
        INightMarketRepository nightMarkets,
        IBoothRepository booths,
        IFoodItemRepository foodItems,
        IMarketLayoutRepository layouts,
        ILogger<ImageUploadService> logger)
    {
        _fileStorage = fileStorage;
        _nightMarkets = nightMarkets;
        _booths = booths;
        _foodItems = foodItems;
        _layouts = layouts;
        _logger = logger;
    }

    public async Task<ApiResponse<object>> UploadMarketThumbnailAsync(
        Guid marketOwnerId, Guid marketId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default)
    {
        var ownerMarkets = await _nightMarkets.GetByOwnerIdAsync(marketOwnerId, ct);
        var market = ownerMarkets.FirstOrDefault(m => m.Id == marketId);
        if (market is null)
            throw AppException.Forbidden("You do not own this night market.", "MARKET_NOT_OWNED");

        var url = await _fileStorage.SaveImageAsync("market-thumbnail", stream, fileName, contentType, length, ct);
        return ApiResponse<object>.SuccessResponse(new { url }, "Market thumbnail uploaded successfully.");
    }

    public async Task<ApiResponse<object>> UploadBoothThumbnailByMarketOwnerAsync(
        Guid marketOwnerId, Guid boothId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null)
            throw AppException.NotFound("Booth not found.", "BOOTH_NOT_FOUND");

        var ownerMarkets = await _nightMarkets.GetByOwnerIdAsync(marketOwnerId, ct);
        if (!ownerMarkets.Any(m => m.Id == booth.NightMarketId))
            throw AppException.Forbidden("You do not own the night market this booth belongs to.", "BOOTH_NOT_IN_OWN_MARKET");

        var previousUrl = booth.ThumbnailUrl;
        var url = await _fileStorage.SaveImageAsync("booth-thumbnail", stream, fileName, contentType, length, ct);
        booth.ThumbnailUrl = url;
        booth.UpdatedAt = DateTime.UtcNow;
        _booths.Update(booth);
        await _booths.SaveChangesAsync();
        await DeletePreviousImageBestEffortAsync(previousUrl, ct);
        return ApiResponse<object>.SuccessResponse(new { url }, "Booth thumbnail uploaded successfully.");
    }

    public async Task<ApiResponse<object>> UploadBoothThumbnailByBoothOwnerAsync(
        Guid boothOwnerId, Guid boothId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default)
    {
        var booth = await _booths.GetOwnedBoothAsync(boothOwnerId, boothId);
        if (booth is null)
            throw AppException.NotFound("Booth not found or you do not own this booth.", "BOOTH_NOT_FOUND");

        var previousUrl = booth.ThumbnailUrl;
        var url = await _fileStorage.SaveImageAsync("booth-thumbnail", stream, fileName, contentType, length, ct);
        booth.ThumbnailUrl = url;
        booth.UpdatedAt = DateTime.UtcNow;
        _booths.Update(booth);
        await _booths.SaveChangesAsync();
        await DeletePreviousImageBestEffortAsync(previousUrl, ct);
        return ApiResponse<object>.SuccessResponse(new { url }, "Booth thumbnail uploaded successfully.");
    }

    public async Task<ApiResponse<object>> UploadFoodItemThumbnailAsync(
        Guid boothOwnerId, Guid foodItemId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default)
    {
        var foodItem = await _foodItems.GetByIdAsync(foodItemId);
        if (foodItem is null)
            throw AppException.NotFound("Food item not found.", "FOOD_ITEM_NOT_FOUND");

        var booth = await _booths.GetOwnedBoothAsync(boothOwnerId, foodItem.BoothId);
        if (booth is null)
            throw AppException.Forbidden("You do not own the booth this food item belongs to.", "BOOTH_NOT_OWNED");

        var url = await _fileStorage.SaveImageAsync("food-thumbnail", stream, fileName, contentType, length, ct);
        return ApiResponse<object>.SuccessResponse(new { url }, "Food item thumbnail uploaded successfully.");
    }

    public async Task<ApiResponse<object>> UploadLayoutImageAsync(
        Guid marketOwnerId, Guid layoutId, Stream stream, string fileName, string contentType, long length, CancellationToken ct = default)
    {
        var layout = await _layouts.GetByIdAsync(layoutId);
        if (layout is null)
            throw AppException.NotFound("Layout not found.", "LAYOUT_NOT_FOUND");

        var ownerMarkets = await _nightMarkets.GetByOwnerIdAsync(marketOwnerId, ct);
        if (!ownerMarkets.Any(m => m.Id == layout.NightMarketId))
            throw AppException.Forbidden("You do not own the night market this layout belongs to.", "MARKET_NOT_OWNED");

        var url = await _fileStorage.SaveImageAsync("layout-image", stream, fileName, contentType, length, ct);
        return ApiResponse<object>.SuccessResponse(new { url }, "Layout image uploaded successfully.");
    }

    private async Task DeletePreviousImageBestEffortAsync(string? oldUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(oldUrl))
            return;

        try
        {
            await _fileStorage.DeleteImageIfManagedAsync(oldUrl, ct);
        }
        catch (Exception ex)
        {
            // The database already points at the new image. A cleanup failure
            // must not make the successful replacement look failed to the client.
            _logger.LogWarning(ex, "Could not remove replaced image {ImageUrl}.", oldUrl);
        }
    }
}
