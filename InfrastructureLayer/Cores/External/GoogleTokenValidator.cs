using DomainLayer.InterfaceCore.External;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace InfrastructureLayer.Cores.External;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;

    public GoogleTokenValidator(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _clientId = configuration["Google:ClientId"] ?? string.Empty;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_clientId)) 
            throw new InvalidOperationException("Google:ClientId is not configured.");

        var response = await _httpClient.GetFromJsonAsync<GoogleTokenInfo>($"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}", cancellationToken);

        if (response is null || 
            response.Audience != _clientId || 
            !string.Equals(response.EmailVerified, "true", StringComparison.OrdinalIgnoreCase) || 
            string.IsNullOrWhiteSpace(response.Email)) 
            return null;

        return new GoogleUserInfo(response.Email.Trim().ToLowerInvariant(), response.Name ?? response.Email, response.Picture);
    }

    private class GoogleTokenInfo
    {
        [JsonPropertyName("aud")]
        public string? Audience { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("email_verified")]
        public string? EmailVerified { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("picture")]
        public string? Picture { get; init; }
    }
}
