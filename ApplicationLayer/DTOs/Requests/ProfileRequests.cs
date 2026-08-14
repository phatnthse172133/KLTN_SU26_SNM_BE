using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Common;

namespace ApplicationLayer.DTOs.Requests;

public class UpdateProfileRequest
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(255)]
    public string? Address { get; set; }

    public DateOnly? DoB { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = PasswordPolicy.TooShortMessage)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Confirmation password does not match the new password.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
