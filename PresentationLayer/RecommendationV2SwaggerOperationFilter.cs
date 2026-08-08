using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PresentationLayer;

public sealed class RecommendationV2SwaggerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;
        if (path.StartsWith("api/customer/ai/v2/meal-plans", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMealPlan(operation, path, context.ApiDescription.HttpMethod ?? string.Empty);
            return;
        }
        if (!path.StartsWith("api/customer/ai/v2/recommendations", StringComparison.OrdinalIgnoreCase)) return;

        if (operation.RequestBody?.Content.TryGetValue("application/json", out var media) == true)
        {
            media.Example = path.EndsWith("feedback", StringComparison.OrdinalIgnoreCase)
                ? new OpenApiObject
                {
                    ["foodId"] = new OpenApiString("11111111-1111-1111-1111-111111111111"),
                    ["action"] = new OpenApiString("LIKED")
                }
                : new OpenApiObject
                {
                    ["query"] = new OpenApiString("Tôi muốn bò nướng cay nhẹ dưới 100000 đồng, gần tôi"),
                    ["latitude"] = new OpenApiDouble(10.7769),
                    ["longitude"] = new OpenApiDouble(106.7009),
                    ["maxDistanceMeters"] = new OpenApiInteger(5000),
                    ["maximumPrice"] = new OpenApiDouble(100000),
                    ["page"] = new OpenApiInteger(1),
                    ["pageSize"] = new OpenApiInteger(10)
                };
        }

        if (operation.Responses.TryGetValue("200", out var success))
        {
            success.Description = path.EndsWith("feedback", StringComparison.OrdinalIgnoreCase)
                ? "Feedback recorded after owner, expiry, and session-result membership validation."
                : "Recommendation result. Business statuses include SUCCESS, NO_STRONG_MATCH, NO_SUITABLE_RESULTS, and LOCATION_REQUIRED.";
            if (success.Content.TryGetValue("application/json", out var successMedia))
            {
                successMedia.Examples = path.EndsWith("feedback", StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<string, OpenApiExample>
                    {
                        ["validFeedback"] = new() { Summary = "Valid latest-state feedback", Value = FeedbackSuccess() }
                    }
                    : new Dictionary<string, OpenApiExample>
                    {
                        ["strongMatches"] = Example("SUCCESS", false, true, false),
                        ["nearMatchesOnly"] = Example("NO_STRONG_MATCH", false, false, true),
                        ["noSuitableResults"] = Example("NO_SUITABLE_RESULTS", false, false, false),
                        ["locationRequired"] = Example("LOCATION_REQUIRED", false, false, false, "LOCATION_REQUIRED"),
                        ["providerFallback"] = Example("SUCCESS", true, true, false, "PROVIDER_FALLBACK_USED")
                    };
            }
        }
        if (operation.Responses.TryGetValue("422", out var unprocessable))
            unprocessable.Description = "Normalized intent is insufficient/invalid, or feedback is expired/not a member of the session.";
        if (operation.Responses.TryGetValue("429", out var rateLimited))
            rateLimited.Description = "Per-customer AI V2 rate limit exceeded (AI_RATE_LIMITED).";
        if (operation.Responses.TryGetValue("503", out var unavailable))
            unavailable.Description = "Only returned when deterministic fallback cannot keep the request serviceable.";
        SetErrorExample(operation, "400", "AI_INVALID_REQUEST");
        SetErrorExample(operation, "401", "UNAUTHORIZED");
        SetErrorExample(operation, "403", "FORBIDDEN");
        SetErrorExample(operation, "404", "AI_RECOMMENDATION_SESSION_NOT_FOUND");
        SetErrorExample(operation, "429", "AI_RATE_LIMITED");
        SetErrorExample(operation, "503", "AI_PROVIDER_UNAVAILABLE");
        if (operation.Responses.TryGetValue("422", out var response422)
            && response422.Content.TryGetValue("application/json", out var media422))
        {
            media422.Examples = path.EndsWith("feedback", StringComparison.OrdinalIgnoreCase)
                ? new Dictionary<string, OpenApiExample>
                {
                    ["expired"] = new() { Value = Error("AI_RECOMMENDATION_SESSION_EXPIRED") },
                    ["foodNotInSession"] = new() { Value = Error("AI_RECOMMENDATION_FOOD_NOT_IN_SESSION") }
                }
                : new Dictionary<string, OpenApiExample>
                {
                    ["fallbackInsufficient"] = new() { Value = Error("AI_FALLBACK_PARSE_INSUFFICIENT") }
                };
        }
    }

    private static void ApplyMealPlan(OpenApiOperation operation, string path, string method)
    {
        operation.Description = "Meal Plan V2 is deterministic and backend-authoritative. Food, booth, market, price, distance, serving, availability and score are never provider-authored. All plan items remain in one market.";
        var isCreate = method == "POST" && path.EndsWith("meal-plans", StringComparison.OrdinalIgnoreCase);
        var isReplace = method == "PUT";
        var isRegenerate = method == "POST" && path.EndsWith("regenerate", StringComparison.OrdinalIgnoreCase);
        if (operation.RequestBody?.Content.TryGetValue("application/json", out var media) == true)
        {
            media.Example = isCreate ? new OpenApiObject
            {
                ["partySize"] = new OpenApiInteger(4), ["budget"] = new OpenApiDouble(450000),
                ["diningStyle"] = new OpenApiString("FOOD_TOUR"), ["request"] = new OpenApiString("Hải sản nướng, ít cay"),
                ["latitude"] = new OpenApiDouble(10.7769), ["longitude"] = new OpenApiDouble(106.7009),
                ["maxDistanceMeters"] = new OpenApiInteger(5000), ["idempotencyKey"] = new OpenApiString("meal-create-001")
            } : isReplace ? new OpenApiObject
            {
                ["replacementFoodId"] = new OpenApiString("11111111-1111-1111-1111-111111111111"), ["expectedPlanVersion"] = new OpenApiInteger(3)
            } : isRegenerate ? new OpenApiObject { ["expectedPlanVersion"] = new OpenApiInteger(4) } : media.Example;
        }
        if (operation.Responses.TryGetValue("200", out var success))
            success.Description = isCreate
                ? "SUCCESS, PARTIAL_PLANS, or NO_FEASIBLE_PLAN. Fewer than three plans is valid; no synthetic plan is added."
                : "Owner-scoped snapshot/current-state detail or a server-recalculated mutation result.";
        SetErrorExample(operation, "400", "AI_INVALID_REQUEST");
        SetErrorExample(operation, "404", "AI_PLAN_NOT_FOUND");
        SetErrorExample(operation, "409", "AI_PLAN_VERSION_CONFLICT");
        SetErrorExample(operation, "422", "AI_PLAN_EXPIRED");
        SetErrorExample(operation, "429", "AI_RATE_LIMITED");
    }

    private static OpenApiExample Example(string status, bool fallback, bool strong, bool near, string? warning = null) => new()
    {
        Summary = status,
        Value = new OpenApiObject
        {
            ["success"] = new OpenApiBoolean(true),
            ["message"] = new OpenApiString("Success"),
            ["data"] = new OpenApiObject
            {
                ["sessionId"] = new OpenApiString("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ["status"] = new OpenApiString(status),
                ["usedProviderFallback"] = new OpenApiBoolean(fallback),
                ["understoodRequest"] = new OpenApiObject
                {
                    ["summary"] = new OpenApiString("bò nướng cay nhẹ dưới 100000 đồng"),
                    ["maximumPrice"] = new OpenApiDouble(100000),
                    ["preferNearMe"] = new OpenApiBoolean(false),
                    ["warnings"] = warning is null ? new OpenApiArray() : new OpenApiArray { new OpenApiString(warning) }
                },
                ["items"] = strong ? new OpenApiArray { RecommendationItem("STRONG_MATCH", 82.5) } : new OpenApiArray(),
                ["nearMatches"] = near ? new OpenApiArray { RecommendationItem("NEAR_MATCH", 68.0) } : new OpenApiArray(),
                ["paging"] = new OpenApiObject
                {
                    ["page"] = new OpenApiInteger(1), ["pageSize"] = new OpenApiInteger(10),
                    ["totalStrongMatches"] = new OpenApiInteger(strong ? 1 : 0),
                    ["totalNearMatches"] = new OpenApiInteger(near ? 1 : 0)
                },
                ["warnings"] = warning is null ? new OpenApiArray() : new OpenApiArray { new OpenApiString(warning) }
            }
        }
    };

    private static OpenApiObject RecommendationItem(string tier, double score) => new()
    {
        ["foodId"] = new OpenApiString("11111111-1111-1111-1111-111111111111"),
        ["foodName"] = new OpenApiString("Bò nướng"), ["currentPrice"] = new OpenApiDouble(80000),
        ["compatibilityScore"] = new OpenApiDouble(score), ["matchTier"] = new OpenApiString(tier),
        ["reason"] = new OpenApiString("Có thịt bò, nướng và giá 80000 nằm trong ngân sách."),
        ["booth"] = new OpenApiObject { ["id"] = new OpenApiString("22222222-2222-2222-2222-222222222222"), ["name"] = new OpenApiString("Gian Bò Nướng") },
        ["market"] = new OpenApiObject { ["id"] = new OpenApiString("33333333-3333-3333-3333-333333333333"), ["name"] = new OpenApiString("Chợ Đêm A"), ["distanceMeters"] = new OpenApiInteger(850) }
    };

    private static OpenApiObject FeedbackSuccess() => new()
    {
        ["success"] = new OpenApiBoolean(true), ["message"] = new OpenApiString("Success"),
        ["data"] = new OpenApiObject
        {
            ["feedbackId"] = new OpenApiString("44444444-4444-4444-4444-444444444444"),
            ["sessionId"] = new OpenApiString("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ["foodId"] = new OpenApiString("11111111-1111-1111-1111-111111111111"),
            ["action"] = new OpenApiString("LIKED")
        }
    };

    private static void SetErrorExample(OpenApiOperation operation, string status, string code)
    {
        if (operation.Responses.TryGetValue(status, out var response)
            && response.Content.TryGetValue("application/json", out var media)) media.Example = Error(code);
    }

    private static OpenApiObject Error(string code) => new()
    {
        ["success"] = new OpenApiBoolean(false),
        ["message"] = new OpenApiString("Request could not be completed."),
        ["errorCode"] = new OpenApiString(code),
        ["data"] = new OpenApiObject { ["errorCode"] = new OpenApiString(code), ["traceId"] = new OpenApiString("trace-id") }
    };
}
