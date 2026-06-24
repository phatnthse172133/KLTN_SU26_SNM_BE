using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.NightMarkets;

public interface INightMarketService
{
    Task<ApiResponse<PaginationResp<NightMarketResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<NightMarketResponse>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<NightMarketResponse>> CreateAsync(CreateNightMarketRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<NightMarketResponse>> UpdateAsync(Guid id, UpdateNightMarketRequest request, CancellationToken cancellationToken = default);
}
