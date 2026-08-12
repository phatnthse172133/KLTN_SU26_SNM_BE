namespace ApplicationLayer.DTOs.Responses;

public sealed class MarketOwnerAccountInvitationResponse
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = "MarketOwner";
    public string Status { get; init; } = string.Empty;
    public bool MustChangePassword { get; init; }
    public bool InvitationQueued { get; init; }
    public string InvitationStatus { get; init; } = string.Empty;
    public DateTime? InvitationSentAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
