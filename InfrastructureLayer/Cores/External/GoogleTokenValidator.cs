using DomainLayer.InterfaceCore.External;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace InfrastructureLayer.Cores.External;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly string _clientId;

    public GoogleTokenValidator(IConfiguration configuration)
    {
        _clientId = configuration["Google:ClientId"]
            ?? configuration["Google OAuth:ClientId"]
            ?? string.Empty;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_clientId)) 
            throw new InvalidOperationException("Google:ClientId or Google OAuth:ClientId is not configured.");

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _clientId }
                });

            cancellationToken.ThrowIfCancellationRequested();
            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
                return null;

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email.Trim().ToLowerInvariant(),
                payload.Name ?? payload.Email,
                payload.Picture);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
