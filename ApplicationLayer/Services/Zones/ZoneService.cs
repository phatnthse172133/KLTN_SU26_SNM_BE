using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Subscriptions;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Zones;

public class ZoneService : IZoneService
{
    private readonly IZoneRepository _zones;
    private readonly INightMarketRepository _nightMarkets;
    private readonly IMapper _mapper;
    private readonly ISubscriptionEntitlementService _entitlements;

    public ZoneService(IZoneRepository zones, INightMarketRepository nightMarkets, IMapper mapper, ISubscriptionEntitlementService entitlements)
    {
        _zones = zones;
        _nightMarkets = nightMarkets;
        _mapper = mapper;
        _entitlements = entitlements;
    }

    public async Task<ApiResponse<PaginationResp<ZoneResponse>>> GetAllAsync(
        Guid nightMarketId, ZoneListRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var market = await EnsureNightMarketExistsAsync(nightMarketId, cancellationToken);
        EnsureOwnership(market, actorId);
        var page = await _zones.GetActivePagedAsync(
            nightMarketId, request.Keyword, request.Status, request.Page, request.PageSize,
            request.SortBy, request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        var slotCounts = await _zones.GetAssignedSlotCountsByMarketAsync(nightMarketId, cancellationToken);
        var responses = page.Items.Select(z =>
        {
            var resp = _mapper.Map<ZoneResponse>(z);
            resp.AssignedSlotCount = slotCounts.TryGetValue(z.Id, out var c) ? c : 0;
            return resp;
        }).ToList();

        return ApiResponse<PaginationResp<ZoneResponse>>.SuccessResponse(
            _mapper.MapPage<Zone, ZoneResponse>(page, request, responses));
    }

    public async Task<ApiResponse<ZoneResponse>> GetByIdAsync(Guid zoneId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var zone = await GetActiveZoneAsync(zoneId, cancellationToken);
        await EnsureZoneOwnershipAsync(zone, actorId, cancellationToken);
        var slotCounts = await _zones.GetAssignedSlotCountsByMarketAsync(zone.NightMarketId, cancellationToken);
        var resp = _mapper.Map<ZoneResponse>(zone);
        resp.AssignedSlotCount = slotCounts.TryGetValue(zone.Id, out var c) ? c : 0;
        return ApiResponse<ZoneResponse>.SuccessResponse(resp);
    }

    public async Task<ApiResponse<ZoneResponse>> CreateAsync(
        Guid nightMarketId, CreateZoneRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var market = await EnsureNightMarketExistsAsync(nightMarketId, cancellationToken);
        EnsureOwnership(market, actorId);
        if (market.MarketOwnerId.HasValue)
        {
            await _entitlements.RequireMarketFeatureAsync(
                market.MarketOwnerId.Value,
                e => e.ZoneManagement,
                "Your current package does not include zone management. Please upgrade to Market Pro or Enterprise.",
                "ZONE_MANAGEMENT_NOT_INCLUDED");
        }
        await ValidateZoneIdentityAsync(nightMarketId, request.ZoneName, request.ZoneCode, null, cancellationToken);

        var now = DateTime.UtcNow;
        var zone = _mapper.Map<Zone>(request);
        zone.Id = Guid.NewGuid();
        zone.NightMarketId = nightMarketId;
        zone.IsDeleted = false;
        zone.CreatedAt = now;
        zone.UpdatedAt = now;

        await _zones.AddAsync(zone);
        await _zones.SaveChangesAsync();
        var resp = _mapper.Map<ZoneResponse>(zone);
        return ApiResponse<ZoneResponse>.SuccessResponse(resp, "Zone created successfully.");
    }

    public async Task<ApiResponse<ZoneResponse>> UpdateAsync(
        Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var zone = await GetActiveZoneAsync(zoneId, cancellationToken);
        await EnsureZoneOwnershipAsync(zone, actorId, cancellationToken);
        await ValidateZoneIdentityAsync(zone.NightMarketId, request.ZoneName, request.ZoneCode, zoneId, cancellationToken);

        _mapper.Map(request, zone);
        zone.UpdatedAt = DateTime.UtcNow;
        _zones.Update(zone);

        await _zones.SaveChangesAsync();
        var resp = _mapper.Map<ZoneResponse>(zone);
        return ApiResponse<ZoneResponse>.SuccessResponse(resp, "Zone updated successfully.");
    }

    public async Task<ApiResponse<ZoneResponse>> UpdateStatusAsync(
        Guid zoneId, UpdateZoneStatusRequest request, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var zone = await GetActiveZoneAsync(zoneId, cancellationToken);
        await EnsureZoneOwnershipAsync(zone, actorId, cancellationToken);
        zone.Status = request.Status;
        zone.UpdatedAt = DateTime.UtcNow;
        _zones.Update(zone);

        await _zones.SaveChangesAsync();
        return ApiResponse<ZoneResponse>.SuccessResponse(_mapper.Map<ZoneResponse>(zone), "Zone status updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid zoneId, CancellationToken cancellationToken = default, Guid? actorId = null)
    {
        var zone = await GetActiveZoneAsync(zoneId, cancellationToken);
        await EnsureZoneOwnershipAsync(zone, actorId, cancellationToken);
        zone.UpdatedAt = DateTime.UtcNow;
        _zones.Delete(zone);

        await _zones.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { zone.Id }, "Zone deleted successfully.");
    }

    private async Task<Zone> GetActiveZoneAsync(Guid id, CancellationToken cancellationToken)
        => await _zones.GetActiveByIdAsync(id, cancellationToken)
           ?? throw AppException.NotFound("Zone was not found.");

    private async Task<NightMarket> EnsureNightMarketExistsAsync(Guid id, CancellationToken cancellationToken)
        => await _nightMarkets.GetActiveByIdAsync(id, cancellationToken)
           ?? throw AppException.NotFound("Night market was not found.");

    private static void EnsureOwnership(NightMarket market, Guid? actorId)
    {
        if (actorId.HasValue && market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to manage this night market's zones.");
    }

    private async Task EnsureZoneOwnershipAsync(Zone zone, Guid? actorId, CancellationToken cancellationToken)
    {
        if (!actorId.HasValue) return;
        var market = await _nightMarkets.GetActiveByIdAsync(zone.NightMarketId, cancellationToken);
        if (market is null || market.MarketOwnerId != actorId)
            throw AppException.Forbidden("You do not have permission to manage this night market's zones.");
    }

    private async Task ValidateZoneIdentityAsync(
        Guid nightMarketId, string name, string? zoneCode, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw AppException.BadRequest("Zone name is required.");

        if (await _zones.ActiveNameExistsAsync(nightMarketId, name, excludeId, cancellationToken))
            throw AppException.Conflict("Zone name already exists in this night market.");

        if (!string.IsNullOrWhiteSpace(zoneCode))
        {
            if (await _zones.ActiveZoneCodeExistsAsync(nightMarketId, zoneCode, excludeId, cancellationToken))
                throw AppException.Conflict($"Zone code '{zoneCode.ToUpperInvariant()}' already exists in this night market.");
        }
    }
}
