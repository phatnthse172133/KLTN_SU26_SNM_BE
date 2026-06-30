using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Zones;

public class ZoneService : IZoneService
{
    private readonly IZoneRepository _zones;
    private readonly INightMarketRepository _nightMarkets;
    private readonly IMapper _mapper;

    public ZoneService(IZoneRepository zones, INightMarketRepository nightMarkets, IMapper mapper)
    {
        _zones = zones;
        _nightMarkets = nightMarkets;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PaginationResp<ZoneResponse>>> GetAllAsync(
        Guid nightMarketId, ZoneListRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNightMarketExistsAsync(nightMarketId, cancellationToken);
        var page = await _zones.GetActivePagedAsync(
            nightMarketId, request.Keyword, request.Status, request.Page, request.PageSize,
            request.SortBy, request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        return ApiResponse<PaginationResp<ZoneResponse>>.SuccessResponse(
            _mapper.MapPage<Zone, ZoneResponse>(page, request));
    }

    public async Task<ApiResponse<ZoneResponse>> GetByIdAsync(Guid zoneId, CancellationToken cancellationToken = default)
    {
        var zone = await GetActiveZoneAsync(zoneId, cancellationToken);
        return ApiResponse<ZoneResponse>.SuccessResponse(_mapper.Map<ZoneResponse>(zone));
    }

    public async Task<ApiResponse<ZoneResponse>> CreateAsync(
        Guid nightMarketId, CreateZoneRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNightMarketExistsAsync(nightMarketId, cancellationToken);
        await ValidateNameAsync(nightMarketId, request.ZoneName, null, cancellationToken);

        var now = DateTime.UtcNow;
        var zone = _mapper.Map<Zone>(request);
        zone.Id = Guid.NewGuid();
        zone.NightMarketId = nightMarketId;
        zone.IsDeleted = false;
        zone.CreatedAt = now;
        zone.UpdatedAt = now;

        await _zones.AddAsync(zone);
        await _zones.SaveChangesAsync();
        return ApiResponse<ZoneResponse>.SuccessResponse(_mapper.Map<ZoneResponse>(zone), "Zone created successfully.");
    }

    public async Task<ApiResponse<ZoneResponse>> UpdateAsync(
        Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken = default)
    {
        var zone = await GetActiveZoneAsync(zoneId, cancellationToken);
        await ValidateNameAsync(zone.NightMarketId, request.ZoneName, zoneId, cancellationToken);

        _mapper.Map(request, zone);
        zone.UpdatedAt = DateTime.UtcNow;
        _zones.Update(zone);

        await _zones.SaveChangesAsync();
        return ApiResponse<ZoneResponse>.SuccessResponse(_mapper.Map<ZoneResponse>(zone), "Zone updated successfully.");
    }

    public async Task<ApiResponse<ZoneResponse>> UpdateStatusAsync(
        Guid zoneId, UpdateZoneStatusRequest request, CancellationToken cancellationToken = default)
    {
        var zone = await GetActiveZoneAsync(zoneId, cancellationToken);
        zone.Status = request.Status;
        zone.UpdatedAt = DateTime.UtcNow;
        _zones.Update(zone);

        await _zones.SaveChangesAsync();
        return ApiResponse<ZoneResponse>.SuccessResponse(_mapper.Map<ZoneResponse>(zone), "Zone status updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid zoneId, CancellationToken cancellationToken = default)
    {
        var zone = await GetActiveZoneAsync(zoneId, cancellationToken);
        zone.UpdatedAt = DateTime.UtcNow;
        _zones.Delete(zone);

        await _zones.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { zone.Id }, "Zone deleted successfully.");
    }

    private async Task<Zone> GetActiveZoneAsync(Guid id, CancellationToken cancellationToken)
        => await _zones.GetActiveByIdAsync(id, cancellationToken)
           ?? throw AppException.NotFound("Zone was not found.");

    private async Task EnsureNightMarketExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await _nightMarkets.GetActiveByIdAsync(id, cancellationToken) is null)
            throw AppException.NotFound("Night market was not found.");
    }

    private async Task ValidateNameAsync(
        Guid nightMarketId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw AppException.BadRequest("Zone name is required.");

        if (await _zones.ActiveNameExistsAsync(nightMarketId, name, excludeId, cancellationToken))
            throw AppException.Conflict("Zone name already exists in this night market.");
    }
}
