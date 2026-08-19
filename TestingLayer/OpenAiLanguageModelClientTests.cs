using System.Net;
using System.Text;
using System.Text.Json;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Assistant;
using InfrastructureLayer.Cores.Assistant;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TestingLayer;

public sealed class AssistantOpenAiLanguageModelClientTests
{
    [Fact]
    public async Task CompleteJson_MissingApiKey_Throws503_WithoutHttpCall()
    {
        var handler = new RecordingHandler();
        var client = Create(handler, apiKey: "");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None, null));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(AssistantErrors.MissingKey, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public async Task CompleteJson_Disabled_Throws503()
    {
        var handler = new RecordingHandler();
        var client = Create(handler, apiKey: "test-key", enabled: false);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None, null));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(AssistantErrors.Disabled, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public async Task CompleteJson_HttpFailure_Throws503()
    {
        var handler = new RecordingHandler
        {
            StatusCode = HttpStatusCode.ServiceUnavailable,
            Body = """{"error":"unavailable"}"""
        };
        var client = Create(handler, apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None, null));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(AssistantErrors.HttpError, AssistantProviderFailure.Reason(exception));
        Assert.Equal(503, AssistantProviderFailure.HttpStatus(exception));
    }

    [Fact]
    public async Task CompleteJson_Unauthorized_Throws503_WithAuthReason()
    {
        var handler = new RecordingHandler
        {
            StatusCode = HttpStatusCode.Unauthorized,
            Body = """{"error":"invalid_api_key"}"""
        };
        var client = Create(handler, apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None, null));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.HttpAuth, AssistantProviderFailure.Reason(exception));
        Assert.Equal(401, AssistantProviderFailure.HttpStatus(exception));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CompleteJson_InvalidProviderJson_Throws503()
    {
        var handler = new RecordingHandler { Body = "{not-json" };
        var client = Create(handler, apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None, null));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantLlmStages.ProviderEnvelope, AssistantProviderFailure.Stage(exception));
        Assert.Equal(AssistantJsonParseClassifier.OutputTruncated, AssistantProviderFailure.ParseFailureCategory(exception));
    }

    [Fact]
    public async Task CompleteJson_Timeout_Throws503()
    {
        var handler = new RecordingHandler { Delay = TimeSpan.FromSeconds(3) };
        var client = Create(handler, apiKey: "test-key", timeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None, null));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.Timeout, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public async Task CompleteJson_JsonSchemaStrict_UsesStructuredResponseFormat()
    {
        var handler = new RecordingHandler();
        var client = Create(handler, apiKey: "test-key");
        var schema = AssistantSemanticSchemaBuilder.Build(3);

        await client.CompleteJsonAsync(
            "sys",
            "user",
            2500,
            CancellationToken.None,
            new LanguageModelJsonSchemaOptions { SchemaJson = schema, Name = "semantic_scores", Strict = true });

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("json_schema", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Equal("semantic_scores", body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("name").GetString());
        Assert.True(body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.Equal(3, body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema").GetProperty("properties").GetProperty("scores").GetProperty("minItems").GetInt32());
    }

    [Fact]
    public async Task CompleteJson_StageBudget_IsNotClampedByGlobalMaxOutputTokens()
    {
        var handler = new RecordingHandler();
        var client = Create(handler, apiKey: "test-key", maxOutputTokens: 400);

        var completion = await client.CompleteJsonAsync("sys", "user", 2500, CancellationToken.None, null);

        Assert.Equal("{}", completion.Content);
        Assert.Equal("stop", completion.FinishReason);
        Assert.Equal(2500, completion.ConfiguredMaxOutputTokens);
        Assert.Equal(12, completion.OutputTokenCount);
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(2500, body.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("json_object", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task CompleteJson_FinishReasonLength_ReturnsContentWithDiagnostics_AndRetriesOnce()
    {
        var handler = new RecordingHandler
        {
            Bodies =
            [
                """{"model":"gpt-4o-mini","choices":[{"finish_reason":"length","message":{"content":"{\"scores\":["}}],"usage":{"completion_tokens":400}}""",
                """{"model":"gpt-4o-mini","choices":[{"finish_reason":"length","message":{"content":"{\"scores\":["}}],"usage":{"completion_tokens":400}}"""
            ]
        };
        var client = Create(handler, apiKey: "test-key", retryCount: 1);

        var completion = await client.CompleteJsonAsync("sys", "user", 2500, CancellationToken.None, null);

        Assert.Equal(2, handler.Calls);
        Assert.Equal("length", completion.FinishReason);
        Assert.Equal(400, completion.OutputTokenCount);
        Assert.Equal(2500, completion.ConfiguredMaxOutputTokens);
        Assert.Equal("""{"scores":[""", completion.Content);
    }

    [Fact]
    public void ResolveOutputTokens_UsesRequestedStageBudget()
    {
        Assert.Equal(2500, OpenAiLanguageModelClient.ResolveOutputTokens(2500, 400));
        Assert.Equal(900, OpenAiLanguageModelClient.ResolveOutputTokens(900, 400));
        Assert.Equal(400, OpenAiLanguageModelClient.ResolveOutputTokens(0, 400));
    }

    private static OpenAiLanguageModelClient Create(
        HttpMessageHandler handler,
        string apiKey,
        bool enabled = true,
        int timeoutSeconds = 20,
        int maxOutputTokens = 400,
        int retryCount = 0)
        => new(
            new HttpClient(handler),
            Options.Create(new OpenAiOptions
            {
                Enabled = enabled,
                ApiKey = apiKey,
                TimeoutSeconds = timeoutSeconds,
                BaseUrl = "https://api.openai.com/v1",
                Model = "gpt-4o-mini",
                MaxOutputTokens = maxOutputTokens,
                RetryCount = retryCount
            }),
            NullLogger<OpenAiLanguageModelClient>.Instance);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Calls;
        public TimeSpan Delay;
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = """{"model":"gpt-4o-mini","choices":[{"finish_reason":"stop","message":{"content":"{}"}}],"usage":{"completion_tokens":12}}""";
        public string[]? Bodies { get; set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref Calls);
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            if (Delay > TimeSpan.Zero)
                await Task.Delay(Delay, cancellationToken);
            var body = Bodies is { Length: > 0 }
                ? Bodies[Math.Min(call - 1, Bodies.Length - 1)]
                : Body;
            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
