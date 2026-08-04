namespace InfrastructureLayer.Cores.Helppers;

// Single source of truth for the identity data embedded in an access token.
public class JwtPayload
{
    public Guid UserId { get; set; }

    public string Email { get; set; }

    public string Role { get; set; }

    public string TokenId { get; set; }

    public JwtPayload(Guid userId, string email, string role, string tokenId)
    {
        UserId = userId;
        Email = email;
        Role = role;
        TokenId = tokenId;
    }
}
