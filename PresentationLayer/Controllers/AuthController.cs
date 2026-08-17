using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PresentationLayer.Helpers;

namespace PresentationLayer.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register/customer")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> RegisterCustomer(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterCustomerAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("register/booth-owner")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> RegisterBoothOwner(RegisterBoothOwnerRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterBoothOwnerAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("login")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [HttpPost("google-login")]
    [EnableRateLimiting("AuthAbusePolicy")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.GoogleLoginAsync(request, cancellationToken);
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [HttpGet("verify-email")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string? token, CancellationToken cancellationToken)
    {
        var wantsHtml = EmailVerificationHtml.WantsHtml(Request);
        try
        {
            var response = await _authService.VerifyEmailAsync(token ?? string.Empty, cancellationToken);
            return wantsHtml
                ? EmailVerificationHtml.Page(success: true, StatusCodes.Status200OK)
                : response.Success ? Ok(response) : BadRequest(response);
        }
        catch (AppException exception) when (wantsHtml)
        {
            return EmailVerificationHtml.Page(success: false, exception.StatusCode);
        }
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.ResendVerificationAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("verify-password-reset-otp")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> VerifyPasswordResetOtp(VerifyPasswordResetOtpRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.VerifyPasswordResetOtpAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.ResetPasswordAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("reset-password-by-token")]
    [EnableRateLimiting("AuthAbusePolicy")]
    public async Task<IActionResult> ResetPasswordByToken(ResetPasswordByTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.ResetPasswordByTokenAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("refresh-token")]
    [EnableRateLimiting("AuthSessionPolicy")]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(request, cancellationToken);
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [EnableRateLimiting("AuthSessionPolicy")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LogoutAsync(request, cancellationToken);
        return Ok(response);
    }
}
