using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Account;


public interface IAccountService
{
    Task<ApiResponse<UserResponse>> GetMyAccountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserResponse>> UpdateMyAccountAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserResponse>> UpdateAvatarAsync(Guid userId, UpdateAvatarRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PaginationResp<ManagedUserResponse>>> GetUsersAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<ManagedUserResponse>> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ManagedUserResponse>> ChangeUserStatusAsync(Guid adminId, Guid userId, ChangeUserStatusRequest request, CancellationToken cancellationToken = default);
}
