using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Profile;

public interface IProfileService
{
    Task<ApiResponse<UserResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserResponse>> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
