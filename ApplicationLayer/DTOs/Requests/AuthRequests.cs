using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Common;

namespace ApplicationLayer.DTOs.Requests;

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.TooShortMessage)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Confirmation password does not match the password.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class RegisterCustomerRequest : RegisterRequest
{
}

public sealed class RegisterBoothOwnerRequest : RegisterRequest
{
}

public class LoginRequest
{
    [Required]
    public string EmailOrUserName { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    [Required, StringLength(8192)]
    public string IdToken { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    [Required, StringLength(256)]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    [Required, StringLength(256)]
    public string RefreshToken { get; set; } = string.Empty;

    [StringLength(4096)]
    public string? DeviceToken { get; set; }
}

public class ResendVerificationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class VerifyPasswordResetOtpRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression("^\\d{6}$", ErrorMessage = "OTP must contain exactly 6 digits.")]
    public string Otp { get; set; } = string.Empty;
}

public class ResetPasswordRequest : VerifyPasswordResetOtpRequest
{
    [Required, StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.TooShortMessage)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Confirmation password does not match the new password.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ResetPasswordByTokenRequest
{
    [Required, StringLength(4096)]
    public string Token { get; set; } = string.Empty;

    [Required, StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.TooShortMessage)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Confirmation password does not match the new password.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
