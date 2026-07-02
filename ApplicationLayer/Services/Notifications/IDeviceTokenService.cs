using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Notifications;

public interface IDeviceTokenService
{
    Task<ApiResponse<DeviceTokenResponse>> RegisterAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid userId, string? token, string? deviceId, CancellationToken cancellationToken = default);

    Task RemoveAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
