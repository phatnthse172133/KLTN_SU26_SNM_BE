namespace ApplicationLayer.DTOs.Responses;

public class AuthResponse
{
    public string AccessToken { get; set; }

    public string RefreshToken { get; set; }

    public DateTime AccessTokenExpiresAt { get; set; }

    public string Role { get; set; }

    public UserResponse User { get; set; }

    public AuthResponse(
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt,
        string role,
        UserResponse user)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        Role = role;
        User = user;
    }
}

public class UserResponse
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}
