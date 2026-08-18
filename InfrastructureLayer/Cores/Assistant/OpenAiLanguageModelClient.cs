using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplicationLayer.Services.Assistant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Cores.Assistant;

public sealed class OpenAiLanguageModelClient : ILanguageModelClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiLanguageModelClient> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public OpenAiLanguageModelClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiLanguageModelClient> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<LanguageModelJsonCompletion> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.Disabled);
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw AssistantErrors.ProviderUnavailable(AssistantErrors.MissingKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        var outputTokens = ResolveOutputTokens(maxOutputTokens);
        var boundedUser = TruncateInput(userPrompt);
        var extraAttempts = Math.Clamp(_options.RetryCount, 0, 1);
        LanguageModelJsonCompletion? lastCompletion = null;

        for (var attempt = 0; attempt <= extraAttempts; attempt++)
        {
            using var request = CreateRequest(boundedUser, systemPrompt, outputTokens);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, timeout.Token);
            }
            catch (OperationCanceledException exception)
            {
                _logger.LogWarning(exception, "OpenAI request timed out or was canceled.");
                throw AssistantErrors.ProviderUnavailable(AssistantErrors.Timeout, exception);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "OpenAI HTTP request failed.");
                throw AssistantErrors.ProviderUnavailable(AssistantErrors.HttpTransport, exception);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenAI returned {Status}: {Body}", (int)response.StatusCode, Truncate(body));
                    var status = (int)response.StatusCode;
                    var reason = status switch
                    {
                        401 or 403 => AssistantErrors.HttpAuth,
                        429 => AssistantErrors.HttpRateLimit,
                        _ => AssistantErrors.HttpError
                    };
                    throw AssistantErrors.ProviderUnavailable(reason, httpStatus: status);
                }

                OpenAiChatResponse? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(body, JsonOptions);
                }
                catch (JsonException exception)
                {
                    var envelope = LanguageModelJsonCompletion.FromContent(body, configuredMaxOutputTokens: outputTokens);
                    envelope = WithRequestMetadata(envelope, outputTokens, null);
                    if (attempt < extraAttempts)
                    {
                        _logger.LogWarning(exception, "OpenAI envelope JSON was invalid; retrying once. RequestId={RequestId}", envelope.RequestId);
                        continue;
                    }

                    throw AssistantErrors.InvalidProviderJson(
                        AssistantLlmStages.ProviderEnvelope,
                        envelope,
                        AssistantJsonParseClassifier.Classify(body, null, exception),
                        exception,
                        _logger);
                }

                var choice = parsed?.Choices?.FirstOrDefault();
                var content = choice?.Message?.Content?.Trim() ?? string.Empty;
                lastCompletion = WithRequestMetadata(
                    new LanguageModelJsonCompletion
                    {
                        Content = content,
                        Model = string.IsNullOrWhiteSpace(parsed?.Model) ? _options.Model : parsed!.Model!,
                        FinishReason = choice?.FinishReason,
                        ConfiguredMaxOutputTokens = outputTokens,
                        OutputTokenCount = parsed?.Usage?.CompletionTokens,
                        ResponseCharacterCount = content.Length
                    },
                    outputTokens,
                    parsed?.Usage?.CompletionTokens);

                if (ShouldRetryTruncation(lastCompletion.FinishReason, attempt, extraAttempts))
                {
                    _logger.LogWarning(
                        "OpenAI finish_reason=length; retrying once. StageBudget={ConfiguredMaxOutputTokens} OutputTokenCount={OutputTokenCount} RequestId={RequestId}",
                        lastCompletion.ConfiguredMaxOutputTokens,
                        lastCompletion.OutputTokenCount,
                        lastCompletion.RequestId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(content))
                    throw AssistantErrors.ProviderUnavailable(AssistantErrors.EmptyContent);

                return lastCompletion;
            }
        }

        return lastCompletion ?? throw AssistantErrors.ProviderUnavailable(AssistantErrors.EmptyContent);
    }

    public static int ResolveOutputTokens(int requested, int fallbackMaxOutputTokens)
    {
        if (requested > 0)
            return requested;
        return Math.Max(1, fallbackMaxOutputTokens);
    }

    private int ResolveOutputTokens(int requested)
        => ResolveOutputTokens(requested, _options.MaxOutputTokens);

    private bool ShouldRetryTruncation(string? finishReason, int attempt, int extraAttempts)
        => attempt < extraAttempts
            && string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase);

    private HttpRequestMessage CreateRequest(string userPrompt, string systemPrompt, int outputTokens)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Combine("chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        request.Content = JsonContent.Create(new
        {
            model = _options.Model,
            temperature = 0,
            max_tokens = outputTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });
        return request;
    }

    private LanguageModelJsonCompletion WithRequestMetadata(
        LanguageModelJsonCompletion completion,
        int outputTokens,
        int? outputTokenCount)
        => new()
        {
            Content = completion.Content,
            Model = string.IsNullOrWhiteSpace(completion.Model) ? _options.Model : completion.Model,
            FinishReason = completion.FinishReason,
            ConfiguredMaxOutputTokens = outputTokens,
            OutputTokenCount = outputTokenCount ?? completion.OutputTokenCount,
            ResponseCharacterCount = completion.ResponseCharacterCount,
            RequestId = _httpContextAccessor?.HttpContext?.TraceIdentifier
        };

    private string TruncateInput(string value)
    {
        var max = Math.Max(1, _options.MaxInputCharacters);
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value;
        return value[..max];
    }

    private Uri Combine(string path)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.openai.com/v1/"
            : _options.BaseUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(baseUrl), path);
    }

    private static string Truncate(string value)
        => value.Length <= 400 ? value : value[..400];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class OpenAiChatResponse
    {
        public string? Model { get; set; }
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public string? FinishReason { get; set; }
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }

    private sealed class OpenAiUsage
    {
        public int? CompletionTokens { get; set; }
    }
}
