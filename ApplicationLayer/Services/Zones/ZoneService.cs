using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Zones;

public class ZoneService : IZoneService
{
    private readonly IGenericRepository<Zone> _zones;
    private readonly IGenericRepository<NightMarket> _nightMarkets;
    private readonly IMapper _mapper;

    public ZoneService(IGenericRepository<Zone> zones, IGenericRepository<NightMarket> nightMarkets, IMapper mapper)
    {
        _zones = zones;
        _nightMarkets = nightMarkets;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PaginationResp<ZoneResponse>>> GetAllAsync(PaginationReq pagination, Guid? nightMarketId = null, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _zones.GetPagedAsync(
            nightMarketId.HasValue ? zone => zone.NightMarketId == nightMarketId.Value : null,
            pagination.Page,
            pagination.PageSize,
            zone => zone.CreatedAt,
            ascending: false);

        return ApiResponse<PaginationResp<ZoneResponse>>.SuccessResponse(new PaginationResp<ZoneResponse>
        {
            Items = _mapper.Map<List<ZoneResponse>>(items),
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            Total = total
        });
    }

    public async Task<ApiResponse<ZoneResponse>> GetByIdAsync(Guid zoneId, CancellationToken cancellationToken = default)
    {
        var zone = await _zones.GetByIdAsync(zoneId);
        return zone is null
            ? throw AppException.NotFound("Zone was not found.")
            : ApiResponse<ZoneResponse>.SuccessResponse(_mapper.Map<ZoneResponse>(zone));
    }

    public async Task<ApiResponse<ZoneResponse>> CreateAsync(CreateZoneRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request);

        var now = DateTime.UtcNow;
        var zone = _mapper.Map<Zone>(request);
        zone.Id = Guid.NewGuid();
        zone.CreatedAt = now;
        zone.UpdatedAt = now;

        await _zones.AddAsync(zone);
        await _zones.SaveChangesAsync();

        return ApiResponse<ZoneResponse>.SuccessResponse(_mapper.Map<ZoneResponse>(zone), "Zone created successfully.");
    }

    public async Task<ApiResponse<ZoneResponse>> UpdateAsync(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken = default)
    {
        var zone = await _zones.GetByIdAsync(zoneId);
        if (zone is null)
            throw AppException.NotFound("Zone was not found.");

        await ValidateAsync(request, zoneId);

        _mapper.Map(request, zone);
        zone.UpdatedAt = DateTime.UtcNow;

        _zones.Update(zone);
        await _zones.SaveChangesAsync();

        return ApiResponse<ZoneResponse>.SuccessResponse(_mapper.Map<ZoneResponse>(zone), "Zone updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid zoneId, CancellationToken cancellationToken = default)
    {
        var zone = await _zones.GetByIdAsync(zoneId);
        if (zone is null)
            throw AppException.NotFound("Zone was not found.");

        _zones.Delete(zone);
        await _zones.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { zone.Id }, "Zone deleted successfully.");
    }

    private async Task ValidateAsync(CreateZoneRequest request, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(request.ZoneName))
            throw AppException.BadRequest("Zone name is required.");

        if (!await _nightMarkets.AnyAsync(nightMarket => nightMarket.Id == request.NightMarketId))
            throw AppException.NotFound("Night market was not found.");

        var name = request.ZoneName.Trim();
        var exists = await _zones.AnyAsync(zone =>
            zone.NightMarketId == request.NightMarketId &&
            zone.ZoneName.ToLower() == name.ToLower() &&
            (!excludeId.HasValue || zone.Id != excludeId.Value));

        if (exists)
            throw AppException.Conflict("Zone name already exists in this night market.");
    }
}
