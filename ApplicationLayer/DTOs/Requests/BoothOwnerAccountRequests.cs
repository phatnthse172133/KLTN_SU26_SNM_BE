using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CreateBoothOwnerAccountRequest
{
    [Required(ErrorMessage = "Booth Owner email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid Booth Owner email address.")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;
}

public sealed class BoothOwnerAccountListRequest : PaginationReq
{
    [StringLength(150)]
    public string? Keyword { get; set; }

    [StringLength(32)]
    public string? InvitationStatus { get; set; }
}
