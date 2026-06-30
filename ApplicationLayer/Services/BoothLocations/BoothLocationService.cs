using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.BoothLocations;

public class BoothLocationService : IBoothLocationService
{
    private readonly IBoothLocationRepository _locations;
    private readonly ILayoutNodeRepository _nodes;
    private readonly IMarketLayoutRepository _layouts;
    private readonly IBoothRepository _booths;
    private readonly IZoneRepository _zones;
    private readonly IMapper _mapper;

    public BoothLocationService(IBoothLocationRepository locations, ILayoutNodeRepository nodes,
        IMarketLayoutRepository layouts, IBoothRepository booths, IZoneRepository zones, IMapper mapper)
        => (_locations, _nodes, _layouts, _booths, _zones, _mapper) = (locations, nodes, layouts, booths, zones, mapper);

    public async Task<ApiResponse<PaginationResp<BoothLocationResponse>>> GetByLayoutAsync(Guid layoutId, Guid? zoneId, PaginationReq request, CancellationToken cancellationToken = default)
    {
        await EnsureLayoutAsync(layoutId, cancellationToken);
        await ValidateZoneFilterAsync(layoutId, zoneId, cancellationToken);
        var page = await _locations.GetPagedByLayoutAsync(layoutId, zoneId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<BoothLocationResponse>>.SuccessResponse(
            _mapper.MapPage<BoothLocation, BoothLocationResponse>(page, request));
    }

    public async Task<ApiResponse<PaginationResp<LayoutNodeResponse>>> GetAvailableAsync(Guid layoutId, Guid? zoneId, PaginationReq request, CancellationToken cancellationToken = default)
    {
        await EnsureLayoutAsync(layoutId, cancellationToken);
        await ValidateZoneFilterAsync(layoutId, zoneId, cancellationToken);
        var page = await _nodes.GetAvailableBoothAccessPagedAsync(
            layoutId, zoneId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<LayoutNodeResponse>>.SuccessResponse(
            _mapper.MapPage<LayoutNode, LayoutNodeResponse>(page, request));
    }

    public async Task<ApiResponse<BoothLocationResponse>> GetByBoothAsync(Guid boothId, CancellationToken cancellationToken = default)
        => ApiResponse<BoothLocationResponse>.SuccessResponse(_mapper.Map<BoothLocationResponse>(
            await _locations.GetCurrentByBoothAsync(boothId, cancellationToken) ?? throw AppException.NotFound("The booth does not have an active location.")));

    public async Task<ApiResponse<NodeAvailabilityResponse>> GetAvailabilityAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        if (await _nodes.GetActiveByIdAsync(nodeId, cancellationToken) is null) throw AppException.NotFound("Layout node was not found.");
        var location = await _locations.GetCurrentByNodeAsync(nodeId, cancellationToken);
        return ApiResponse<NodeAvailabilityResponse>.SuccessResponse(new()
        {
            NodeId = nodeId, IsAvailable = location is null, BoothId = location?.BoothId
        });
    }

    public async Task<ApiResponse<BoothLocationResponse>> AssignAsync(Guid boothId, AssignBoothLocationRequest request, CancellationToken cancellationToken = default)
    {
        if (await _locations.GetCurrentByBoothAsync(boothId, cancellationToken) is not null)
            throw AppException.Conflict("The booth already has a location. Use the move endpoint.");
        return await SaveAsync(boothId, request, false, cancellationToken);
    }

    public async Task<ApiResponse<BoothLocationResponse>> MoveAsync(Guid boothId, AssignBoothLocationRequest request, CancellationToken cancellationToken = default)
    {
        if (await _locations.GetCurrentByBoothAsync(boothId, cancellationToken) is null)
            throw AppException.NotFound("The booth does not have an active location.");
        return await SaveAsync(boothId, request, true, cancellationToken);
    }

    public async Task<ApiResponse<object>> ReleaseAsync(Guid boothId, CancellationToken cancellationToken = default)
    {
        if (await _locations.GetCurrentByBoothAsync(boothId, cancellationToken) is null)
            throw AppException.NotFound("The booth does not have an active location.");
        await _locations.ReleaseAsync(boothId, DateTime.UtcNow, cancellationToken);
        return ApiResponse<object>.SuccessResponse(new { BoothId = boothId }, "Booth location released successfully.");
    }

    private async Task<ApiResponse<BoothLocationResponse>> SaveAsync(Guid boothId, AssignBoothLocationRequest request, bool moving, CancellationToken token)
    {
        var booth = await _booths.GetByIdAsync(boothId) ?? throw AppException.NotFound("Booth was not found.");
        var layout = await EnsureLayoutAsync(request.LayoutId, token);
        var node = await _nodes.GetActiveByIdAsync(request.LayoutNodeId, token) ?? throw AppException.NotFound("Layout node was not found.");
        if (layout.NightMarketId != booth.NightMarketId || node.LayoutId != layout.Id)
            throw AppException.BadRequest("Booth, layout and node must belong to the same night market layout.");
        if (node.NodeType != LayoutNodeType.BoothAccess) throw AppException.BadRequest("Booths can only be assigned to BoothAccess nodes.");
        var occupied = await _locations.GetCurrentByNodeAsync(node.Id, token);
        if (occupied is not null && occupied.BoothId != boothId) throw AppException.Conflict("The selected node is occupied.");
        if (request.ZoneId.HasValue)
        {
            var zone = await _zones.GetActiveByIdAsync(request.ZoneId.Value, token) ?? throw AppException.NotFound("Zone was not found.");
            if (zone.NightMarketId != booth.NightMarketId) throw AppException.BadRequest("Zone does not belong to the booth's night market.");
            if (node.ZoneId.HasValue && node.ZoneId != zone.Id) throw AppException.BadRequest("Node does not belong to the selected zone.");
        }
        var now = DateTime.UtcNow;
        var location = new BoothLocation
        {
            Id = Guid.NewGuid(), BoothId = boothId, LayoutId = layout.Id, LayoutNodeId = node.Id,
            ZoneId = request.ZoneId ?? node.ZoneId, SlotNumber = request.SlotNumber?.Trim(),
            Xcoordinate = node.Xcoordinate, Ycoordinate = node.Ycoordinate, IsDeleted = false,
            CreatedAt = now, UpdatedAt = now
        };
        await _locations.AssignOrMoveAsync(location, now, token);
        return ApiResponse<BoothLocationResponse>.SuccessResponse(_mapper.Map<BoothLocationResponse>(location),
            moving ? "Booth location moved successfully." : "Booth location assigned successfully.");
    }

    private async Task<MarketLayout> EnsureLayoutAsync(Guid id, CancellationToken token)
        => await _layouts.GetActiveByIdAsync(id, token) ?? throw AppException.NotFound("Market layout was not found.");

    private async Task ValidateZoneFilterAsync(Guid layoutId, Guid? zoneId, CancellationToken token)
    {
        if (!zoneId.HasValue) return;
        var layout = await EnsureLayoutAsync(layoutId, token);
        var zone = await _zones.GetActiveByIdAsync(zoneId.Value, token);
        if (zone?.NightMarketId != layout.NightMarketId)
            throw AppException.BadRequest("Zone does not belong to the layout's night market.");
    }
}
