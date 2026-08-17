using DomainLayer.InterfaceCore.External;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace InfrastructureLayer.Cores.External;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly string[] _allowedClientIds;

    public GoogleTokenValidator(IConfiguration configuration)
    {
        _allowedClientIds = ReadAllowedClientIds(configuration);
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (_allowedClientIds.Length == 0)
        {
            throw new InvalidOperationException(
                "Google allowed client IDs are not configured. Set Google:AllowedClientIds or Google Web/Android/iOS ClientId.");
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = _allowedClientIds
                });

            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name,
                payload.Picture,
                payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }

    public static string[] ReadAllowedClientIds(IConfiguration configuration)
    {
        var ids = new List<string>();
        Add(ids, configuration["Google:ClientId"]);
        Add(ids, configuration["Google:WebClientId"]);
        Add(ids, configuration["Google:AndroidClientId"]);
        Add(ids, configuration["Google:IosClientId"]);
        Add(ids, configuration["Google OAuth:ClientId"]);

        foreach (var child in configuration.GetSection("Google:AllowedClientIds").GetChildren())
        {
            Add(ids, child.Value);
        }

        return ids
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void Add(List<string> ids, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ids.Add(value.Trim());
        }
    }
}
