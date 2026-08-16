using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplicationLayer.Services.Assistant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Cores.Assistant;

public sealed class OpenAiLanguageModelClient : ILanguageModelClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiLanguageModelClient> _logger;

    public OpenAiLanguageModelClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiLanguageModelClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            throw AssistantErrors.ProviderUnavailable();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        using var request = new HttpRequestMessage(HttpMethod.Post, Combine("chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        request.Content = JsonContent.Create(new
        {
            model = _options.Model,
            temperature = 0,
            max_tokens = maxOutputTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(exception, "OpenAI request timed out or was canceled.");
            throw AssistantErrors.ProviderUnavailable(exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "OpenAI HTTP request failed.");
            throw AssistantErrors.ProviderUnavailable(exception);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI returned {Status}: {Body}", (int)response.StatusCode, Truncate(body));
                throw AssistantErrors.ProviderUnavailable();
            }

            OpenAiChatResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<OpenAiChatResponse>(body, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw AssistantErrors.ProviderUnavailable(exception);
            }

            var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                throw AssistantErrors.ProviderUnavailable();
            return content.Trim();
        }
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
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }
}
