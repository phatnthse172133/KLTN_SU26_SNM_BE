using Xunit;

namespace TestingLayer;

/// <summary>
/// Opt-in live OpenAI suite. Normal <c>dotnet test</c> must never call OpenAI.
/// Enable explicitly:
///   $env:RUN_LIVE_AI_TESTS="true"
///   $env:OpenAI__ApiKey="sk-..."
///   $env:OpenAI__Enabled="true"
///   dotnet test --filter "Category=LiveOpenAI"
/// </summary>
[Trait("Category", "LiveOpenAI")]
public sealed class LiveOpenAiOptInTests
{
    private static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("RUN_LIVE_AI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Live_suite_is_disabled_by_default()
    {
        Assert.False(IsEnabled);
    }

    [Fact]
    public void Live_recommendation_scenarios_require_explicit_opt_in()
    {
        if (!IsEnabled)
        {
            // Scenario checklist for manual/live runs (do not execute HTTP here):
            // 1 phở  2 đói quá  3 ăn gì  4 mưa lạnh muốn ăn gì nóng
            // 5 hải sản cay dưới 100k  6 không cay  7 3 đứa 200k ăn nhẹ
            // 8 đi với người yêu ăn gì  9 I want something refreshing
            // 10 something cheap for three people  11 irrelevant input
            // 12 long contextual Vietnamese  13 refinement after prior request
            return;
        }

        Assert.Fail("Live OpenAI execution is intentionally not automated in this suite. Run manual Swagger/API checks against a configured environment with a strict $5 budget.");
    }
}
