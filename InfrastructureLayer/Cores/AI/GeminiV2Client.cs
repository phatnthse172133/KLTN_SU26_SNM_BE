using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2;
using ApplicationLayer.AI.V2.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Cores.AI;

public sealed class GeminiV2SecretOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public sealed record GeminiJsonResult(bool IsSuccess, AiProviderFailureCategory Category, string? Json, string? ModelName, string? RequestId)
{
    public static GeminiJsonResult Failure(AiProviderFailureCategory category, string? model, string? requestId = null)
        => new(false, category, null, model, requestId);
}

public sealed class GeminiV2Client(HttpClient httpClient, IOptions<AiProviderRuntimeOptions> runtime,
    IOptions<GeminiV2SecretOptions> secret, ILogger<GeminiV2Client> logger)
{
    private readonly AiProviderRuntimeOptions _runtime = runtime.Value;
    private readonly string _apiKey = secret.Value.ApiKey;

    public async Task<GeminiJsonResult> GenerateJsonOnceAsync(string systemInstruction, object payload, object jsonSchema, decimal temperature, CancellationToken cancellationToken)
    {
        if (!_runtime.Enabled || !_runtime.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(_apiKey))
            return GeminiJsonResult.Failure(AiProviderFailureCategory.DISABLED, _runtime.Model);
        if (!TryGetOfficialEndpoint(out var endpoint))
        {
            logger.LogWarning("Gemini V2 request rejected because provider host or model configuration is invalid.");
            return GeminiJsonResult.Failure(AiProviderFailureCategory.PERMANENT_ERROR, _runtime.Model);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_runtime.TimeoutSeconds, 1, 30)));
        var started = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
            request.Content = JsonContent.Create(new
            {
                systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = JsonSerializer.Serialize(payload, WebJson) } } } },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseJsonSchema = jsonSchema,
                    maxOutputTokens = Math.Clamp(_runtime.MaxOutputTokens, 64, 2048),
                    temperature = Math.Clamp(temperature, 0m, 1m)
                }
            });
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var requestId = RequestId(response);
            if (!response.IsSuccessStatusCode)
            {
                var category = response.StatusCode == HttpStatusCode.TooManyRequests ? AiProviderFailureCategory.RATE_LIMITED
                    : (int)response.StatusCode >= 500 ? AiProviderFailureCategory.TRANSIENT_ERROR : AiProviderFailureCategory.PERMANENT_ERROR;
                logger.LogWarning("Gemini V2 request failed. StatusCode={StatusCode} FailureCategory={FailureCategory}",
                    (int)response.StatusCode, category);
                return GeminiJsonResult.Failure(category, _runtime.Model, requestId);
            }
            var json = await CandidateText(response, timeout.Token);
            return string.IsNullOrWhiteSpace(json)
                ? GeminiJsonResult.Failure(AiProviderFailureCategory.INVALID_RESPONSE, _runtime.Model, requestId)
                : new(true, AiProviderFailureCategory.NONE, json, _runtime.Model, requestId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { logger.LogDebug("Gemini V2 request was cancelled by the caller."); return GeminiJsonResult.Failure(AiProviderFailureCategory.CANCELLED, _runtime.Model); }
        catch (OperationCanceledException)
        { logger.LogWarning("Gemini V2 request timed out."); return GeminiJsonResult.Failure(AiProviderFailureCategory.TIMEOUT, _runtime.Model); }
        catch (HttpRequestException)
        { logger.LogWarning("Gemini V2 transport failed."); return GeminiJsonResult.Failure(AiProviderFailureCategory.TRANSIENT_ERROR, _runtime.Model); }
        catch (JsonException)
        { logger.LogWarning("Gemini V2 returned an invalid response envelope."); return GeminiJsonResult.Failure(AiProviderFailureCategory.INVALID_RESPONSE, _runtime.Model); }
        finally
        {
            AiV2Telemetry.ProviderDurationMs.Record(started.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("provider", "Gemini"));
        }
    }

    private bool TryGetOfficialEndpoint(out Uri endpoint)
    {
        endpoint = null!;
        if (!Uri.TryCreate(_runtime.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps
            || !baseUri.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(_runtime.Model, "^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)) return false;
        return Uri.TryCreate($"{baseUri.ToString().TrimEnd('/')}/models/{_runtime.Model}:generateContent", UriKind.Absolute, out endpoint!);
    }

    private static async Task<string?> CandidateText(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        const int maximumEnvelopeBytes = 65_536;
        if (response.Content.Headers.ContentLength > maximumEnvelopeBytes) return null;
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var bounded = new MemoryStream();
        var buffer = new byte[8192];
        while (bounded.Length <= maximumEnvelopeBytes)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (bounded.Length + read > maximumEnvelopeBytes) return null;
            await bounded.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        bounded.Position = 0;
        using var document = await JsonDocument.ParseAsync(bounded, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0
            || !candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0 || !parts[0].TryGetProperty("text", out var text)) return null;
        var candidateText = text.GetString();
        return candidateText?.Length <= 16_384 ? candidateText : null;
    }

    private static string? RequestId(HttpResponseMessage response)
    {
        var value = response.Headers.TryGetValues("x-goog-request-id", out var values) ? values.FirstOrDefault()
            : response.Headers.TryGetValues("x-request-id", out values) ? values.FirstOrDefault() : null;
        return value is { Length: > 0 and <= 200 } && Regex.IsMatch(value, "^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant) ? value : null;
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
}
