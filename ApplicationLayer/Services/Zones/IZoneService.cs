using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Zones;

public interface IZoneService
{
    Task<ApiResponse<PaginationResp<ZoneResponse>>> GetAllAsync(Guid nightMarketId, ZoneListRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<ZoneResponse>> GetByIdAsync(Guid zoneId, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<ZoneResponse>> CreateAsync(Guid nightMarketId, CreateZoneRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<ZoneResponse>> UpdateAsync(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<ZoneResponse>> UpdateStatusAsync(Guid zoneId, UpdateZoneStatusRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<object>> DeleteAsync(Guid zoneId, CancellationToken cancellationToken = default, Guid? actorId = null);
}
