using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CreateMarketOwnerAccountRequest
{
    [Required(ErrorMessage = "Market Owner email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid Market Owner email address.")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;
}
