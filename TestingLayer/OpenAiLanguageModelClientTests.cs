using System.Net;
using System.Text;
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
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None));

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
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(AssistantErrors.Disabled, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public async Task CompleteJson_HttpFailure_Throws503()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"error":"unavailable"}""", Encoding.UTF8, "application/json")
            }
        };
        var client = Create(handler, apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None));

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
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"invalid_api_key"}""", Encoding.UTF8, "application/json")
            }
        };
        var client = Create(handler, apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.HttpAuth, AssistantProviderFailure.Reason(exception));
        Assert.Equal(401, AssistantProviderFailure.HttpStatus(exception));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CompleteJson_InvalidProviderJson_Throws503()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
            }
        };
        var client = Create(handler, apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public async Task CompleteJson_Timeout_Throws503()
    {
        var handler = new RecordingHandler { Delay = TimeSpan.FromSeconds(3) };
        var client = Create(handler, apiKey: "test-key", timeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            client.CompleteJsonAsync("sys", "user", 100, CancellationToken.None));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.Timeout, AssistantProviderFailure.Reason(exception));
    }

    private static OpenAiLanguageModelClient Create(
        HttpMessageHandler handler,
        string apiKey,
        bool enabled = true,
        int timeoutSeconds = 20)
        => new(
            new HttpClient(handler),
            Options.Create(new OpenAiOptions
            {
                Enabled = enabled,
                ApiKey = apiKey,
                TimeoutSeconds = timeoutSeconds,
                BaseUrl = "https://api.openai.com/v1",
                Model = "gpt-4o-mini"
            }),
            NullLogger<OpenAiLanguageModelClient>.Instance);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Calls;
        public TimeSpan Delay;
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"choices":[{"message":{"content":"{}"}}]}""", Encoding.UTF8, "application/json")
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (Delay > TimeSpan.Zero)
                await Task.Delay(Delay, cancellationToken);
            return Response;
        }
    }
}
