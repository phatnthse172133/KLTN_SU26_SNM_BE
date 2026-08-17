namespace DomainLayer.InterfaceCore.External;

public class GoogleUserInfo
{
    public string GoogleId { get; set; }

    public string Email { get; set; }

    public string FullName { get; set; }

    public string? AvatarUrl { get; set; }

    public bool EmailVerified { get; set; }

    public GoogleUserInfo(string googleId, string email, string fullName, string? avatarUrl, bool emailVerified = true)
    {
        GoogleId = googleId;
        Email = email;
        FullName = fullName;
        AvatarUrl = avatarUrl;
        EmailVerified = emailVerified;
    }
}

/// <summary>
/// Production registers <c>GoogleTokenValidator</c>. Tests mock this verifier so AuthService
/// never talks to Google.
/// </summary>
public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
