namespace DomainLayer.InterfaceCore.External;

public class GoogleUserInfo
{
    public string Email { get; set; }

    public string FullName { get; set; }

    public string? AvatarUrl { get; set; }

    public GoogleUserInfo(string email, string fullName, string? avatarUrl)
    {
        Email = email;
        FullName = fullName;
        AvatarUrl = avatarUrl;
    }
}

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
