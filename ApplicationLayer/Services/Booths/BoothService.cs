using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Booths;

public class BoothService : IBoothService
{
    private readonly IBoothRepository _booths;
    private readonly IZoneRepository _zones;
    private readonly IMapper _mapper;
    private readonly IBoothLocationRepository _locations;
    public BoothService(IBoothRepository booths, IZoneRepository zones, IMapper mapper, IBoothLocationRepository locations)
    {
        _booths = booths;
        _zones = zones;
        _mapper = mapper;
        _locations = locations;
    }

    public async Task<ApiResponse<object>> GetMyBoothsAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => ApiResponse<object>.SuccessResponse(_mapper.Map<List<BoothResponse>>(await _booths.FindAsync(b => b.BoothOwnerId == ownerId)));

    public async Task<ApiResponse<BoothResponse>> UpdateMyBoothAsync(Guid ownerId, Guid boothId, UpdateMyBoothRequest request, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null) 
            throw AppException.NotFound("Booth was not found.");

        if (booth.BoothOwnerId != ownerId) 
            throw AppException.Forbidden("You do not have permission to manage this booth.");

        _mapper.Map(request, booth);
        booth.BoothName = booth.BoothName.Trim(); booth.UpdatedAt = DateTime.UtcNow;

        _booths.Update(booth); await _booths.SaveChangesAsync();
        if (booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Closed)
            await _locations.ReleaseAsync(booth.Id, DateTime.UtcNow, cancellationToken);
        return ApiResponse<BoothResponse>.SuccessResponse(_mapper.Map<BoothResponse>(booth), "Booth updated successfully.");
    }

    public async Task<ApiResponse<PaginationResp<BoothResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _booths.GetPagedAsync(null, pagination.Page, pagination.PageSize, b => b.CreatedAt, false);
        return ApiResponse<PaginationResp<BoothResponse>>.SuccessResponse(new PaginationResp<BoothResponse>
        {
            Items = _mapper.Map<List<BoothResponse>>(items), Page = pagination.Page, PageSize = pagination.PageSize, Total = total
        });
    }

    public async Task<ApiResponse<BoothResponse>> UpdateByAdminAsync(Guid boothId, AdminUpdateBoothRequest request, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null) 
            throw AppException.NotFound("Booth was not found.");

        if (request.ZoneId.HasValue && (await _zones.GetActiveByIdAsync(request.ZoneId.Value))?.NightMarketId != booth.NightMarketId)
            throw AppException.BadRequest("The assigned zone does not belong to this booth's night market.");

        _mapper.Map(request, booth);
        booth.BoothName = booth.BoothName.Trim(); booth.UpdatedAt = DateTime.UtcNow;

        _booths.Update(booth); await _booths.SaveChangesAsync();
        if (booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Closed)
            await _locations.ReleaseAsync(booth.Id, DateTime.UtcNow, cancellationToken);
        return ApiResponse<BoothResponse>.SuccessResponse(_mapper.Map<BoothResponse>(booth), "Booth updated successfully by the administrator.");
    }
}
