using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.AI.Services;

public interface IAISettingsService
{
    Task<ApiResponse<AISettingsResponse>> GetAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<AISettingsResponse>> UpdateAsync(UpdateAISettingsRequest request, CancellationToken cancellationToken = default);
}
