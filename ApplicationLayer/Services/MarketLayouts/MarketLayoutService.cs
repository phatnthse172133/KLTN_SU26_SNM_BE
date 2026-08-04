using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

public class MarketLayoutService : IMarketLayoutService
{
    private readonly IMarketLayoutRepository _layouts;
    private readonly IZoneRepository _zones;
    private readonly INightMarketRepository _nightMarkets;
    private readonly IMapper _mapper;
    private readonly ILayoutGraphValidationService _graphValidation;

    public MarketLayoutService(
        IMarketLayoutRepository layouts, IZoneRepository zones,
        INightMarketRepository nightMarkets, IMapper mapper, ILayoutGraphValidationService graphValidation)
    {
        _layouts = layouts;
        _zones = zones;
        _nightMarkets = nightMarkets;
        _mapper = mapper;
        _graphValidation = graphValidation;
    }

    public async Task<ApiResponse<PaginationResp<MarketLayoutResponse>>> GetAllAsync(
        Guid nightMarketId, MarketLayoutListRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNightMarketExistsAsync(nightMarketId, cancellationToken);
        var page = await _layouts.GetActivePagedAsync(
            nightMarketId, request.Keyword, request.Status, request.Page, request.PageSize,
            request.SortBy, request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        return ApiResponse<PaginationResp<MarketLayoutResponse>>.SuccessResponse(
            _mapper.MapPage<MarketLayout, MarketLayoutResponse>(page, request));
    }

    public async Task<ApiResponse<MarketLayoutResponse>> GetByIdAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
        => ApiResponse<MarketLayoutResponse>.SuccessResponse(
            _mapper.Map<MarketLayoutResponse>(await GetActiveLayoutAsync(layoutId, cancellationToken)));

    public async Task<ApiResponse<MarketLayoutResponse>> CreateAsync(
        Guid nightMarketId, CreateMarketLayoutRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNightMarketExistsAsync(nightMarketId, cancellationToken);
        await ValidateIdentityAsync(nightMarketId, request.LayoutName, request.Version, null, cancellationToken);

        var now = DateTime.UtcNow;
        var layout = _mapper.Map<MarketLayout>(request);
        layout.Id = Guid.NewGuid();
        layout.NightMarketId = nightMarketId;
        layout.Status = MarketLayoutStatus.Draft;
        layout.CoordinateUnit = LayoutCoordinateUnit.LayoutUnit;
        layout.MetersPerLayoutUnit = null;
        layout.DistanceCalibrationStatus = DistanceCalibrationStatus.Uncalibrated;
        layout.GraphRevision = 1;
        layout.IsDeleted = false;
        layout.CreatedAt = now;
        layout.UpdatedAt = now;

        await _layouts.AddAsync(layout);
        await _layouts.SaveChangesAsync();
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout created successfully.");
    }

    public async Task<ApiResponse<MarketLayoutResponse>> UpdateAsync(
        Guid layoutId, UpdateMarketLayoutRequest request, CancellationToken cancellationToken = default)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before changing its information.");

        await ValidateIdentityAsync(layout.NightMarketId, request.LayoutName, request.Version, layoutId, cancellationToken);
        _mapper.Map(request, layout);
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout updated successfully.");
    }

    public async Task<ApiResponse<MarketLayoutResponse>> UpdateImageAsync(
        Guid layoutId, UpdateMarketLayoutImageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before replacing its image.");

        _mapper.Map(request, layout);
        layout.GraphRevision = checked(layout.GraphRevision + 1);
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout image updated successfully.");
    }

    public async Task<ApiResponse<MarketLayoutEditorDataResponse>> GetEditorDataAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
    {
        var layout = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
                     ?? throw AppException.NotFound("Market layout was not found.");
        var zones = await _zones.GetActiveByNightMarketIdAsync(layout.NightMarketId, cancellationToken);
        var edges = await _layouts.GetEdgesByLayoutIdAsync(layout.Id, cancellationToken);

        return ApiResponse<MarketLayoutEditorDataResponse>.SuccessResponse(new MarketLayoutEditorDataResponse
        {
            Layout = _mapper.Map<MarketLayoutResponse>(layout),
            Zones = _mapper.Map<List<ZoneResponse>>(zones),
            Nodes = _mapper.Map<List<LayoutNodeResponse>>(layout.LayoutNodes),
            Edges = _mapper.Map<List<LayoutEdgeResponse>>(edges),
            BoothLocations = _mapper.Map<List<BoothLocationResponse>>(layout.BoothLocations)
        });
    }

    public async Task<ApiResponse<MarketLayoutResponse>> UpdateCalibrationAsync(
        Guid layoutId, UpdateLayoutCalibrationRequest request, CancellationToken cancellationToken = default)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Clone the active layout to a draft before changing its distance calibration.", "ACTIVE_LAYOUT_IMMUTABLE");

        if (request.MetersPerLayoutUnit <= 0)
            throw AppException.BadRequest("Meters per layout unit must be greater than zero.", "INVALID_LAYOUT_SCALE");

        layout.CoordinateUnit = LayoutCoordinateUnit.LayoutUnit;
        layout.MetersPerLayoutUnit = request.MetersPerLayoutUnit;
        layout.DistanceCalibrationStatus = DistanceCalibrationStatus.Calibrated;
        layout.GraphRevision = checked(layout.GraphRevision + 1);
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);
        await _layouts.SaveChangesAsync();
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(
            _mapper.Map<MarketLayoutResponse>(layout), "Layout distance calibration updated successfully.");
    }

    public async Task<ApiResponse<MarketLayoutResponse>> CloneDraftAsync(
        Guid layoutId, CloneMarketLayoutDraftRequest request, CancellationToken cancellationToken = default)
    {
        var clone = await _layouts.CloneToDraftAsync(layoutId, request.LayoutName, DateTime.UtcNow, cancellationToken);
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(
            _mapper.Map<MarketLayoutResponse>(clone), "Active layout cloned to a new draft version.");
    }

    public async Task<ApiResponse<MarketLayoutValidationResponse>> ValidateAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
    {
        var result = await _graphValidation.ValidateAsync(layoutId, cancellationToken);
        return ApiResponse<MarketLayoutValidationResponse>.SuccessResponse(result,
            result.IsValid ? "Market layout is valid." : "Market layout validation failed.");
    }

    public async Task<ApiResponse<MarketLayoutResponse>> ActivateAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
    {
        var layout = await _layouts.GetEditorLayoutAsync(layoutId, cancellationToken)
                     ?? throw AppException.NotFound("Market layout was not found.");
        var validation = await _graphValidation.ValidateAsync(layoutId, cancellationToken);
        if (!validation.IsValid)
            throw AppException.BadRequest(string.Join(" ", validation.Errors));

        layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        var now = DateTime.UtcNow;
        await _layouts.ActivateExclusiveAsync(layout.NightMarketId, layout.Id, now, cancellationToken);

        layout.Status = MarketLayoutStatus.Active;
        layout.UpdatedAt = now;
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout activated successfully.");
    }

    public async Task<ApiResponse<MarketLayoutResponse>> DeactivateAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        if (layout.Status != MarketLayoutStatus.Active)
            throw AppException.Conflict("Only an active market layout can be deactivated.");

        layout.Status = MarketLayoutStatus.Inactive;
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Update(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<MarketLayoutResponse>.SuccessResponse(_mapper.Map<MarketLayoutResponse>(layout), "Market layout deactivated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid layoutId, CancellationToken cancellationToken = default)
    {
        var layout = await GetActiveLayoutAsync(layoutId, cancellationToken);
        if (layout.Status == MarketLayoutStatus.Active)
            throw AppException.Conflict("Deactivate the market layout before archiving it.");

        layout.Status = MarketLayoutStatus.Archived;
        layout.UpdatedAt = DateTime.UtcNow;
        _layouts.Delete(layout);

        await _layouts.SaveChangesAsync();
        return ApiResponse<object>.SuccessResponse(new { layout.Id }, "Market layout archived successfully.");
    }

    private async Task<MarketLayout> GetActiveLayoutAsync(Guid id, CancellationToken cancellationToken)
        => await _layouts.GetActiveByIdAsync(id, cancellationToken)
           ?? throw AppException.NotFound("Market layout was not found.");

    private async Task EnsureNightMarketExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await _nightMarkets.GetActiveByIdAsync(id, cancellationToken) is null)
            throw AppException.NotFound("Night market was not found.");
    }

    private async Task ValidateIdentityAsync(
        Guid nightMarketId, string name, int version, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw AppException.BadRequest("Layout name is required.");

        if (version <= 0)
            throw AppException.BadRequest("Layout version must be greater than zero.");

        if (await _layouts.ActiveNameOrVersionExistsAsync(nightMarketId, name, version, excludeId, cancellationToken))
            throw AppException.Conflict("Layout name or version already exists in this night market.");
    }

    private static MarketLayoutValidationResponse BuildValidation(MarketLayout layout)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(layout.LayoutImageUrl))
            errors.Add("Layout image is required.");

        if (layout.Width <= 0 || layout.Height <= 0)
            errors.Add("Layout width and height must be greater than zero.");

        if (layout.LayoutNodes.Count == 0)
            errors.Add("Layout must have at least one node.");

        if (layout.LayoutNodes.Any(node =>
                node.Xcoordinate < 0 || node.Xcoordinate > layout.Width ||
                node.Ycoordinate < 0 || node.Ycoordinate > layout.Height))
            errors.Add("All nodes must be inside the layout dimensions.");

        return new MarketLayoutValidationResponse { Errors = errors, Warnings = warnings };
    }
}
