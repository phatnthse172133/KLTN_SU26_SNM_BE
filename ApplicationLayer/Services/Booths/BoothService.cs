using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Booths;

public class BoothService : IBoothService
{
    private readonly IGenericRepository<Booth> _booths;
    private readonly IGenericRepository<Zone> _zones;
    private readonly IMapper _mapper;
    public BoothService(IGenericRepository<Booth> booths, IGenericRepository<Zone> zones, IMapper mapper) => (_booths, _zones, _mapper) = (booths, zones, mapper);

    public async Task<ApiResponse<object>> GetMyBoothsAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => ApiResponse<object>.SuccessResponse(_mapper.Map<List<BoothResponse>>(await _booths.FindAsync(b => b.BoothOwnerId == ownerId)));

    public async Task<ApiResponse<BoothResponse>> UpdateMyBoothAsync(Guid ownerId, Guid boothId, UpdateMyBoothRequest request, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null) return ApiResponse<BoothResponse>.Failure("Booth was not found.");
        if (booth.BoothOwnerId != ownerId) return ApiResponse<BoothResponse>.Failure("You do not have permission to manage this booth.");
        _mapper.Map(request, booth);
        booth.BoothName = booth.BoothName.Trim(); booth.UpdatedAt = DateTime.UtcNow;
        _booths.Update(booth); await _booths.SaveChangesAsync();
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
        if (booth is null) return ApiResponse<BoothResponse>.Failure("Booth was not found.");
        if (request.ZoneId.HasValue && (await _zones.GetByIdAsync(request.ZoneId.Value))?.NightMarketId != booth.NightMarketId)
            return ApiResponse<BoothResponse>.Failure("The assigned zone does not belong to this booth's night market.");
        _mapper.Map(request, booth);
        booth.BoothName = booth.BoothName.Trim(); booth.UpdatedAt = DateTime.UtcNow;
        _booths.Update(booth); await _booths.SaveChangesAsync();
        return ApiResponse<BoothResponse>.SuccessResponse(_mapper.Map<BoothResponse>(booth), "Booth updated successfully by the administrator.");
    }
}
