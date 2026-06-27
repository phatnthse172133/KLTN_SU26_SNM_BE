namespace DomainLayer.InterfaceCore.External;

public class GoogleUserInfo
{
    public string GoogleId { get; set; }

    public string Email { get; set; }

    public string FullName { get; set; }

    public string? AvatarUrl { get; set; }

    public GoogleUserInfo(string googleId, string email, string fullName, string? avatarUrl)
    {
        GoogleId = googleId;
        Email = email;
        FullName = fullName;
        AvatarUrl = avatarUrl;
    }
}

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
