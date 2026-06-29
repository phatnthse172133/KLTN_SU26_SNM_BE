using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.NightMarkets;

public class NightMarketService : INightMarketService
{
    private readonly INightMarketRepository _markets;
    private readonly IMapper _mapper;

    public NightMarketService(INightMarketRepository markets, IMapper mapper)
    {
        _markets = markets;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PaginationResp<NightMarketResponse>>> GetAllAsync(
        NightMarketListRequest request,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _markets.GetActivePagedAsync(
            request.Keyword,
            request.Status,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        return ApiResponse<PaginationResp<NightMarketResponse>>.SuccessResponse(new PaginationResp<NightMarketResponse>
        {
            Items = _mapper.Map<List<NightMarketResponse>>(items),
            Page = request.Page,
            PageSize = request.PageSize,
            Total = total
        });
    }

    public async Task<ApiResponse<NightMarketResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        return market is null ? throw AppException.NotFound("Night market was not found.")
            : ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market));
    }

    public async Task<ApiResponse<NightMarketResponse>> CreateAsync(CreateNightMarketRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, cancellationToken: cancellationToken);

        var market = _mapper.Map<NightMarket>(request);
        var now = DateTime.UtcNow;
        market.Id = Guid.NewGuid();
        market.IsDeleted = false;
        market.CreatedAt = now;
        market.UpdatedAt = now;

        await _markets.AddAsync(market);
        await _markets.SaveChangesAsync();
        return ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market), "Night market created successfully.");
    }

    public async Task<ApiResponse<NightMarketResponse>> UpdateAsync(Guid id, UpdateNightMarketRequest request, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        await ValidateAsync(request, id, cancellationToken);

        _mapper.Map(request, market);
        market.UpdatedAt = DateTime.UtcNow;

        _markets.Update(market);
        await _markets.SaveChangesAsync();
        return ApiResponse<NightMarketResponse>.SuccessResponse(_mapper.Map<NightMarketResponse>(market), "Night market updated successfully.");
    }

    public async Task<ApiResponse<NightMarketResponse>> UpdateGeographicLocationAsync(
        Guid id,
        UpdateNightMarketGeographicLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        ValidateGeographicLocation(
            request.Address,
            request.Latitude,
            request.Longitude,
            request.BoundaryWidthMeters,
            request.BoundaryHeightMeters);

        _mapper.Map(request, market);
        market.UpdatedAt = DateTime.UtcNow;

        _markets.Update(market);
        await _markets.SaveChangesAsync();

        return ApiResponse<NightMarketResponse>.SuccessResponse(
            _mapper.Map<NightMarketResponse>(market),
            "Night market geographic location updated successfully.");
    }

    public async Task<ApiResponse<NightMarketNavigationInfoResponse>> GetNavigationInfoAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        if (!market.Latitude.HasValue ||
            !market.Longitude.HasValue ||
            !market.BoundaryWidthMeters.HasValue ||
            !market.BoundaryHeightMeters.HasValue)
            throw AppException.BadRequest("Night market geographic information is incomplete.");

        return ApiResponse<NightMarketNavigationInfoResponse>.SuccessResponse(
            _mapper.Map<NightMarketNavigationInfoResponse>(market));
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var market = await GetActiveMarketAsync(id, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        market.UpdatedAt = DateTime.UtcNow;
        _markets.Delete(market);
        await _markets.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { market.Id }, "Night market deleted successfully.");
    }

    private async Task<NightMarket?> GetActiveMarketAsync(Guid id, CancellationToken cancellationToken)
        => await _markets.GetActiveByIdAsync(id, cancellationToken);

    private async Task ValidateAsync(
        CreateNightMarketRequest request,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateGeographicLocation(
            request.Address,
            request.Latitude,
            request.Longitude,
            request.BoundaryWidthMeters,
            request.BoundaryHeightMeters);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw AppException.BadRequest("Night market name is required.");

        if (request.OpeningHours.HasValue != request.ClosingHours.HasValue)
            throw AppException.BadRequest("Opening hours and closing hours must be provided together.");

        if (request.OpeningHours.HasValue &&
            request.ClosingHours.HasValue &&
            request.OpeningHours.Value >= request.ClosingHours.Value)
            throw AppException.BadRequest("Opening hours must be earlier than closing hours for same-day operation.");

        if (await _markets.ActiveNameExistsAsync(request.Name, excludeId, cancellationToken))
            throw AppException.Conflict("Night market name already exists.");
    }

    private static void ValidateGeographicLocation(
        string address,
        decimal? latitude,
        decimal? longitude,
        int boundaryWidthMeters,
        int boundaryHeightMeters)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw AppException.BadRequest("Night market address is required.");

        if (!latitude.HasValue || latitude.Value is < -90 or > 90)
            throw AppException.BadRequest("Latitude must be between -90 and 90.");

        if (!longitude.HasValue || longitude.Value is < -180 or > 180)
            throw AppException.BadRequest("Longitude must be between -180 and 180.");

        if (boundaryWidthMeters <= 0 || boundaryHeightMeters <= 0)
            throw AppException.BadRequest("Boundary width and height must be greater than zero.");
    }
}
