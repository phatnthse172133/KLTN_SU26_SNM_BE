using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApplicationLayer.Services.Notifications;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Cores.Notifications;

public class FirebasePushNotificationService : IPushNotificationService
{
    private const string FirebaseScope =
        "https://www.googleapis.com/auth/firebase.messaging";
    private readonly HttpClient _httpClient;
    private readonly FirebaseSettings _settings;
    private readonly ILogger<FirebasePushNotificationService> _logger;
    private GoogleCredential? _credential;

    public FirebasePushNotificationService(
        HttpClient httpClient,
        IOptions<FirebaseSettings> settings,
        ILogger<FirebasePushNotificationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<PushDeliveryResult> SendAsync(
        IReadOnlyCollection<string> tokens,
        PushNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || tokens.Count == 0)
            return PushDeliveryResult.Empty;

        if (string.IsNullOrWhiteSpace(_settings.ProjectId))
            throw new InvalidOperationException(
                "Firebase:ProjectId is required when Firebase push is enabled.");

        var credential = GetCredential();
        var accessToken = await credential.UnderlyingCredential
            .GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        var invalidTokens = new List<string>();

        foreach (var token in tokens.Distinct())
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{_settings.ProjectId}/messages:send");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new
            {
                message = new
                {
                    token,
                    notification = new
                    {
                        title = message.Title,
                        body = message.Body
                    },
                    data = message.Data
                }
            });

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);
            if (response.IsSuccessStatusCode)
                continue;

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            if (IsInvalidToken(response.StatusCode, error))
                invalidTokens.Add(token);

            _logger.LogWarning(
                "FCM rejected a push request with status {StatusCode}: {Error}",
                response.StatusCode,
                error);
        }

        return new PushDeliveryResult(invalidTokens);
    }

    private GoogleCredential GetCredential()
    {
        if (_credential is not null)
            return _credential;

        GoogleCredential credential;
        if (!string.IsNullOrWhiteSpace(_settings.CredentialsJson))
        {
            credential = CredentialFactory
                .FromJson<ServiceAccountCredential>(_settings.CredentialsJson)
                .ToGoogleCredential();
        }
        else if (!string.IsNullOrWhiteSpace(_settings.CredentialsPath))
        {
            credential = CredentialFactory
                .FromFile<ServiceAccountCredential>(_settings.CredentialsPath)
                .ToGoogleCredential();
        }
        else
        {
            credential = GoogleCredential.GetApplicationDefault();
        }

        _credential = credential.CreateScoped(FirebaseScope);
        return _credential;
    }

    private static bool IsInvalidToken(HttpStatusCode statusCode, string error)
        => statusCode == HttpStatusCode.NotFound
            || error.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
            || error.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase);
}
