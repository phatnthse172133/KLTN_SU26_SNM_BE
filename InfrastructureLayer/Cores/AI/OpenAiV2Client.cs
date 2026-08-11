using System.Net;
using System.Net.Http.Headers;
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

public sealed class OpenAiSecretOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public sealed record OpenAiJsonResult(bool IsSuccess, AiProviderFailureCategory Category, string? Json, string? ModelName, string? RequestId)
{
    public static OpenAiJsonResult Failure(AiProviderFailureCategory category, string? model, string? requestId = null)
        => new(false, category, null, model, requestId);
}

public sealed class OpenAiV2Client(HttpClient httpClient, IOptions<AiProviderRuntimeOptions> runtime,
    IOptions<OpenAiSecretOptions> secret, ILogger<OpenAiV2Client> logger)
{
    private readonly AiProviderRuntimeOptions _runtime = runtime.Value;
    private readonly string _apiKey = secret.Value.ApiKey;
    public bool ApiKeyLoaded => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<OpenAiJsonResult> GenerateJsonOnceAsync(string systemInstruction, object payload, object jsonSchema, decimal temperature, CancellationToken cancellationToken)
    {
        if (!_runtime.Enabled || !_runtime.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(_apiKey))
            return OpenAiJsonResult.Failure(AiProviderFailureCategory.DISABLED, _runtime.Model);
        if (!TryGetOfficialEndpoint(out var endpoint))
        {
            logger.LogWarning("OpenAI V2 request rejected because provider host or model configuration is invalid.");
            return OpenAiJsonResult.Failure(AiProviderFailureCategory.PERMANENT_ERROR, _runtime.Model);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_runtime.TimeoutSeconds, 1, 30)));
        var started = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(BuildRequestBody(systemInstruction, payload, jsonSchema, temperature));
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var requestId = RequestId(response);
            if (!response.IsSuccessStatusCode)
            {
                var category = response.StatusCode == HttpStatusCode.TooManyRequests ? AiProviderFailureCategory.RATE_LIMITED
                    : (int)response.StatusCode >= 500 ? AiProviderFailureCategory.TRANSIENT_ERROR : AiProviderFailureCategory.PERMANENT_ERROR;
                logger.LogWarning("OpenAI V2 request failed. StatusCode={StatusCode} FailureCategory={FailureCategory}",
                    (int)response.StatusCode, category);
                return OpenAiJsonResult.Failure(category, _runtime.Model, requestId);
            }
            var json = await ChoiceContent(response, timeout.Token);
            return string.IsNullOrWhiteSpace(json)
                ? OpenAiJsonResult.Failure(AiProviderFailureCategory.INVALID_RESPONSE, _runtime.Model, requestId)
                : new(true, AiProviderFailureCategory.NONE, json, _runtime.Model, requestId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { logger.LogDebug("OpenAI V2 request was cancelled by the caller."); return OpenAiJsonResult.Failure(AiProviderFailureCategory.CANCELLED, _runtime.Model); }
        catch (OperationCanceledException)
        { logger.LogWarning("OpenAI V2 request timed out."); return OpenAiJsonResult.Failure(AiProviderFailureCategory.TIMEOUT, _runtime.Model); }
        catch (HttpRequestException)
        { logger.LogWarning("OpenAI V2 transport failed."); return OpenAiJsonResult.Failure(AiProviderFailureCategory.TRANSIENT_ERROR, _runtime.Model); }
        catch (JsonException)
        { logger.LogWarning("OpenAI V2 returned an invalid response envelope."); return OpenAiJsonResult.Failure(AiProviderFailureCategory.INVALID_RESPONSE, _runtime.Model); }
        finally
        {
            AiV2Telemetry.ProviderDurationMs.Record(started.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("provider", "OpenAI"));
        }
    }

    private object BuildRequestBody(string systemInstruction, object payload, object jsonSchema, decimal temperature)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = _runtime.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = JsonSerializer.Serialize(payload, WebJson) }
            },
            ["temperature"] = Math.Clamp(temperature, 0m, 1m),
            ["max_tokens"] = Math.Clamp(_runtime.MaxOutputTokens, 64, 2048),
            ["response_format"] = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "structured_output",
                    strict = true,
                    schema = jsonSchema
                }
            }
        };

        if (SupportsReasoningEffort(_runtime.Model) && !string.IsNullOrWhiteSpace(_runtime.ReasoningEffort))
            body["reasoning_effort"] = _runtime.ReasoningEffort.Trim().ToLowerInvariant();

        return body;
    }

    private static bool SupportsReasoningEffort(string model)
    {
        if (string.IsNullOrWhiteSpace(model)) return false;
        var normalized = model.Trim().ToLowerInvariant();
        return normalized.StartsWith("o1", StringComparison.Ordinal)
            || normalized.StartsWith("o3", StringComparison.Ordinal)
            || normalized.StartsWith("o4", StringComparison.Ordinal);
    }

    private bool TryGetOfficialEndpoint(out Uri endpoint)
    {
        endpoint = null!;
        if (!Uri.TryCreate(_runtime.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps
            || !baseUri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(_runtime.Model, "^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)) return false;
        return Uri.TryCreate($"{baseUri.ToString().TrimEnd('/')}/chat/completions", UriKind.Absolute, out endpoint!);
    }

    private static async Task<string?> ChoiceContent(HttpResponseMessage response, CancellationToken cancellationToken)
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
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var content)) return null;
        var text = content.ValueKind == JsonValueKind.String ? content.GetString() : null;
        return text?.Length <= 16_384 ? text : null;
    }

    private static string? RequestId(HttpResponseMessage response)
    {
        var value = response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault()
            : response.Headers.TryGetValues("x-openai-request-id", out values) ? values.FirstOrDefault() : null;
        return value is { Length: > 0 and <= 200 } && Regex.IsMatch(value, "^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant) ? value : null;
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
}
