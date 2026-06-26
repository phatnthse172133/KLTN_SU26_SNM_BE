using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Zones;

public interface IZoneService
{
    Task<ApiResponse<PaginationResp<ZoneResponse>>> GetAllAsync(PaginationReq pagination, Guid? nightMarketId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<ZoneResponse>> GetByIdAsync(Guid zoneId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ZoneResponse>> CreateAsync(CreateZoneRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<ZoneResponse>> UpdateAsync(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteAsync(Guid zoneId, CancellationToken cancellationToken = default);
}
