using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Common;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.CustomerDiscovery;

public sealed class CustomerDiscoveryService : ICustomerDiscoveryService
{
    private readonly IBoothRepository _booths;
    private readonly IFoodItemRepository _foodItems;
    private readonly INightMarketRepository _markets;
    private readonly TimeProvider _timeProvider;

    public CustomerDiscoveryService(IBoothRepository booths, IFoodItemRepository foodItems, INightMarketRepository markets, TimeProvider timeProvider)
        => (_booths, _foodItems, _markets, _timeProvider) = (booths, foodItems, markets, timeProvider);

    public async Task<ApiResponse<PaginationResp<CustomerBoothListItemResponse>>> GetBoothsAsync(CustomerBoothQueryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MarketId.HasValue) await EnsureMarketVisibleAsync(request.MarketId.Value, cancellationToken);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        var page = await _booths.GetCustomerPagedAsync(request.MarketId, NormalizeSearch(request.Search), request.OpenNow, localTime,
            request.MinimumRating, request.MaximumRating, request.Page, request.PageSize, request.Sort, cancellationToken);
        var items = page.Items.Select(item => MapBoothList(item, localTime)).ToList();
        return ApiResponse<PaginationResp<CustomerBoothListItemResponse>>.SuccessResponse(
            PaginationResp<CustomerBoothListItemResponse>.Create(items, page.TotalCount, request));
    }

    public async Task<ApiResponse<CustomerBoothDetailResponse>> GetBoothAsync(Guid boothId, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetCustomerByIdAsync(boothId, cancellationToken)
            ?? throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");
        var localTime = GetLocalTime();
        var listItem = MapBoothList(booth, localTime);
        return ApiResponse<CustomerBoothDetailResponse>.SuccessResponse(new CustomerBoothDetailResponse
        {
            Id = listItem.Id, Name = listItem.Name, ThumbnailUrl = listItem.ThumbnailUrl,
            MarketId = listItem.MarketId, MarketName = listItem.MarketName, SlotNumber = listItem.SlotNumber,
            ZoneId = listItem.ZoneId, ZoneName = listItem.ZoneName, IsOpenNow = listItem.IsOpenNow,
            AverageRating = listItem.AverageRating, ReviewCount = listItem.ReviewCount, FoodCount = listItem.FoodCount,
            IsFeatured = listItem.IsFeatured, Description = booth.Description, PublicPhoneNumber = booth.PublicPhoneNumber,
            OpenTime = booth.OpenTime, CloseTime = booth.CloseTime,
            ImageUrls = BuildImages(booth.ThumbnailUrl, booth.ImageUrls),
            Market = new() { Id = booth.MarketId, Name = booth.MarketName, Address = booth.MarketAddress },
            Location = booth.LayoutId.HasValue && booth.LayoutNodeId.HasValue ? new CustomerBoothLocationResponse
            {
                LayoutId = booth.LayoutId.Value, LayoutNodeId = booth.LayoutNodeId.Value,
                SlotNumber = booth.LocationSlotNumber ?? booth.SlotNumber,
                ZoneId = booth.LocationZoneId ?? booth.ZoneId, ZoneName = booth.LocationZoneName ?? booth.ZoneName
            } : null
        });
    }

    public async Task<ApiResponse<PaginationResp<CustomerFoodListItemResponse>>> GetFoodsAsync(CustomerFoodQueryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MinPrice.HasValue && request.MaxPrice.HasValue && request.MinPrice > request.MaxPrice)
            throw AppException.BadRequest("Minimum price cannot be greater than maximum price.", "INVALID_PRICE_RANGE");
        if (request.MarketId.HasValue) await EnsureMarketVisibleAsync(request.MarketId.Value, cancellationToken);
        if (request.BoothId.HasValue && !await _booths.CustomerVisibleExistsAsync(request.BoothId.Value, cancellationToken))
            throw AppException.NotFound("Booth was not found.", "BOOTH_NOT_FOUND");

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        var page = await _foodItems.GetCustomerPagedAsync(request.MarketId, request.BoothId, request.CategoryId,
            NormalizeSearch(request.Search), request.MinPrice, request.MaxPrice, request.AvailableOnly == true,
            utcNow, localTime, request.Page, request.PageSize, request.Sort, cancellationToken);
        var items = page.Items.Select(item => MapFoodList(item, localTime)).ToList();
        return ApiResponse<PaginationResp<CustomerFoodListItemResponse>>.SuccessResponse(
            PaginationResp<CustomerFoodListItemResponse>.Create(items, page.TotalCount, request));
    }

    public async Task<ApiResponse<CustomerFoodDetailResponse>> GetFoodAsync(Guid foodItemId, CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var food = await _foodItems.GetCustomerByIdAsync(foodItemId, utcNow, cancellationToken)
            ?? throw AppException.NotFound("Food item was not found.", "FOOD_NOT_FOUND");
        var localTime = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(utcNow));
        var listItem = MapFoodList(food, localTime);
        return ApiResponse<CustomerFoodDetailResponse>.SuccessResponse(new CustomerFoodDetailResponse
        {
            Id = listItem.Id, Name = listItem.Name, ThumbnailUrl = listItem.ThumbnailUrl,
            CategoryId = listItem.CategoryId, CategoryName = listItem.CategoryName,
            BasePrice = listItem.BasePrice, EffectivePrice = listItem.EffectivePrice,
            IsAvailable = listItem.IsAvailable, CanOrder = listItem.CanOrder,
            BoothId = listItem.BoothId, BoothName = listItem.BoothName,
            MarketId = listItem.MarketId, MarketName = listItem.MarketName, IsFeatured = listItem.IsFeatured,
            Description = food.Description, ImageUrls = BuildImages(food.ThumbnailUrl, food.ImageUrls),
            Tags = food.Tags.Select(tag => new CustomerFoodTagResponse
            {
                Id = tag.Id, Code = tag.Code, Name = tag.Name, TagGroup = tag.TagGroup.ToString()
            }).ToList(),
            SemanticMetadata = new()
            {
                PrimaryCourse = food.SemanticMetadata.PrimaryCourse,
                SupportedCourses = food.SemanticMetadata.SupportedCourses,
                Ingredients = food.SemanticMetadata.Ingredients.Select(x => new SemanticCatalogResponse(x.Id, x.Code, x.Name)).ToArray(),
                AllergenDeclarations = food.SemanticMetadata.Allergens.Select(x => new FoodAllergenDeclarationResponse(x.Id, x.Code, x.Name, x.DeclarationType, x.IsConfirmed, x.Source)).ToArray(),
                DietaryAttributes = food.SemanticMetadata.Dietary.Select(x => new FoodDietaryAttributeResponse(x.Id, x.Code, x.Name, x.Status, x.IsConfirmed, x.Source)).ToArray(),
                PreparationMethods = food.SemanticMetadata.Preparations.Select(x => new SemanticCatalogResponse(x.Id, x.Code, x.Name)).ToArray(),
                TasteProfiles = food.SemanticMetadata.Tastes.Select(x => new SemanticCatalogResponse(x.Id, x.Code, x.Name)).ToArray(),
                SpiceLevel = food.SpiceLevel, ServingTemperature = food.ServingTemperature, EstimatedServingCount = food.EstimatedServingCount,
                ServingSizeDescription = food.ServingSizeDescription, IsShareable = food.IsShareable
            },
            Booth = new() { Id = food.BoothId, Name = food.BoothName, ThumbnailUrl = food.BoothThumbnailUrl, IsOpenNow = CustomerAvailability.IsOpenNow(food, localTime) },
            Market = new() { Id = food.MarketId, Name = food.MarketName, Address = food.MarketAddress }
        });
    }

    private async Task EnsureMarketVisibleAsync(Guid marketId, CancellationToken cancellationToken)
    {
        if (!await _markets.CustomerVisibleExistsAsync(marketId, cancellationToken))
            throw AppException.NotFound("Night market was not found.", "NIGHT_MARKET_NOT_FOUND");
    }

    private TimeOnly GetLocalTime()
        => TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(_timeProvider.GetUtcNow().UtcDateTime));

    private static CustomerBoothListItemResponse MapBoothList(CustomerBoothReadModel booth, TimeOnly localTime) => new()
    {
        Id = booth.Id, Name = booth.Name, ThumbnailUrl = booth.ThumbnailUrl,
        MarketId = booth.MarketId, MarketName = booth.MarketName,
        SlotNumber = booth.LocationSlotNumber ?? booth.SlotNumber,
        ZoneId = booth.LocationZoneId ?? booth.ZoneId, ZoneName = booth.LocationZoneName ?? booth.ZoneName,
        IsOpenNow = CustomerAvailability.IsOpenNow(booth, localTime), AverageRating = booth.AverageRating,
        ReviewCount = booth.ReviewCount, FoodCount = booth.FoodCount, IsFeatured = booth.IsFeatured
    };

    private static CustomerFoodListItemResponse MapFoodList(CustomerFoodReadModel food, TimeOnly localTime) => new()
    {
        Id = food.Id, Name = food.Name, ThumbnailUrl = food.ThumbnailUrl,
        CategoryId = food.CategoryId, CategoryName = food.CategoryName,
        BasePrice = food.BasePrice, EffectivePrice = food.EffectivePrice,
        IsAvailable = food.IsAvailable, CanOrder = food.IsAvailable && CustomerAvailability.IsOpenNow(food, localTime),
        BoothId = food.BoothId, BoothName = food.BoothName,
        MarketId = food.MarketId, MarketName = food.MarketName, IsFeatured = food.IsFeatured,
        AverageRating = food.AverageRating, ReviewCount = food.ReviewCount,
        PrimaryCourse = food.PrimaryCourse, EstimatedServingCount = food.EstimatedServingCount, IsShareable = food.IsShareable
    };

    private static string? NormalizeSearch(string? search) => string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static IReadOnlyCollection<string> BuildImages(string? thumbnailUrl, IReadOnlyCollection<string> gallery)
        => (string.IsNullOrWhiteSpace(thumbnailUrl) ? gallery : new[] { thumbnailUrl }.Concat(gallery))
            .Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.Ordinal).ToList();
}
