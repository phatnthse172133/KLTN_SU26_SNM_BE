using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CreateBoothOwnerAccountRequest
{
    [Required(ErrorMessage = "Booth Owner email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid Booth Owner email address.")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;
}
