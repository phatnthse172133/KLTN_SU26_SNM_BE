using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Auth;

public interface IAuthService
{
    Task<ApiResponse<object>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> RegisterBoothOwnerAsync(RegisterBoothOwnerRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AuthResponse>> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> VerifyPasswordResetOtpAsync(VerifyPasswordResetOtpRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> ResetPasswordByTokenAsync(ResetPasswordByTokenRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
