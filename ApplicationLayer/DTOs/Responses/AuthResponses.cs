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

    public string UserName { get; set; }

    public string FullName { get; set; }

    public string Email { get; set; }

    public string Role { get; set; }

    public string Status { get; set; }

    public string? AvatarUrl { get; set; }

    public UserResponse(
        Guid id,
        string userName,
        string fullName,
        string email,
        string role,
        string status,
        string? avatarUrl)
    {
        Id = id;
        UserName = userName;
        FullName = fullName;
        Email = email;
        Role = role;
        Status = status;
        AvatarUrl = avatarUrl;
    }
}
