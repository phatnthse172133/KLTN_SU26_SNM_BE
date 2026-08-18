using ApplicationLayer.Services.Assistant;

namespace TestingLayer;

public sealed class AssistantJsonParseClassifierTests
{
    [Fact]
    public void Classify_FinishReasonLength_IsOutputTruncated()
        => Assert.Equal(
            AssistantJsonParseClassifier.OutputTruncated,
            AssistantJsonParseClassifier.Classify("""{"scores":[""", "length"));

    [Fact]
    public void Classify_UnbalancedObject_IsOutputTruncated()
        => Assert.Equal(
            AssistantJsonParseClassifier.OutputTruncated,
            AssistantJsonParseClassifier.Classify("""{"intent":"FOOD_RECOMMENDATION","hardConstraints":{""", "stop"));

    [Fact]
    public void Classify_MarkdownFence_IsMarkdownCodeFence()
        => Assert.Equal(
            AssistantJsonParseClassifier.MarkdownCodeFence,
            AssistantJsonParseClassifier.Classify("```json\n{}\n```", "stop"));

    [Fact]
    public void Classify_Garbage_IsMalformedJson()
        => Assert.Equal(
            AssistantJsonParseClassifier.MalformedJson,
            AssistantJsonParseClassifier.Classify("not-json{{{", "stop", new System.Text.Json.JsonException("invalid")));

    [Fact]
    public void Classify_Empty_IsEmptyProviderResponse()
        => Assert.Equal(
            AssistantJsonParseClassifier.EmptyProviderResponse,
            AssistantJsonParseClassifier.Classify("  ", "stop"));
}
