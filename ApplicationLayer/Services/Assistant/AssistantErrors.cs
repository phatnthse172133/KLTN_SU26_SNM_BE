using ApplicationLayer.Exceptions;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.Assistant;

public static class AssistantErrors
{
    public static AppException InvalidRequest(string message)
        => AppException.BadRequest(message, "ASSISTANT_INVALID_REQUEST");

    public static AppException ConversationNotFound()
        => AppException.NotFound("Assistant conversation was not found.", "ASSISTANT_CONVERSATION_NOT_FOUND");

    public static AppException LocationRequired()
        => AppException.UnprocessableEntity(
            "Current location is required to continue this request.",
            "ASSISTANT_LOCATION_REQUIRED");

    public const string MissingKey = "missing_key";
    public const string Disabled = "disabled";
    public const string Timeout = "timeout";
    public const string HttpAuth = "http_401";
    public const string HttpRateLimit = "http_429";
    public const string HttpError = "http_error";
    public const string HttpTransport = "http_transport";
    public const string InvalidJson = "invalid_json";
    public const string EmptyContent = "empty_content";
    public const string HallucinatedId = "hallucinated_id";
    public const string DuplicateId = "duplicate_id";
    public const string IncompleteIdSet = "incomplete_id_set";

    public static AppException ProviderUnavailable(Exception? inner = null)
        => ProviderUnavailable("unspecified", inner);

    public static AppException ProviderUnavailable(string reason, Exception? inner = null, int? httpStatus = null)
        => ProviderUnavailable(reason, inner, httpStatus, diagnostics: null);

    public static AppException InvalidProviderJson(
        string stage,
        LanguageModelJsonCompletion? completion,
        string parseFailureCategory,
        Exception? inner = null,
        ILogger? logger = null)
    {
        var diagnostics = AssistantProviderDiagnostics.From(stage, completion, parseFailureCategory);
        LogInvalidJson(logger, diagnostics, inner);
        return ProviderUnavailable(InvalidJson, inner, httpStatus: null, diagnostics);
    }

    public static AppException ProviderUnavailable(
        string reason,
        Exception? inner,
        int? httpStatus,
        AssistantProviderDiagnostics? diagnostics)
    {
        object details;
        if (diagnostics is null)
        {
            details = httpStatus is null
                ? new { reason }
                : new { reason, httpStatus };
        }
        else
        {
            details = httpStatus is null
                ? new
                {
                    reason,
                    stage = diagnostics.Stage,
                    finishReason = diagnostics.FinishReason,
                    parseFailureCategory = diagnostics.ParseFailureCategory,
                    configuredMaxOutputTokens = diagnostics.ConfiguredMaxOutputTokens,
                    outputTokenCount = diagnostics.OutputTokenCount,
                    responseCharacterCount = diagnostics.ResponseCharacterCount
                }
                : new
                {
                    reason,
                    httpStatus,
                    stage = diagnostics.Stage,
                    finishReason = diagnostics.FinishReason,
                    parseFailureCategory = diagnostics.ParseFailureCategory,
                    configuredMaxOutputTokens = diagnostics.ConfiguredMaxOutputTokens,
                    outputTokenCount = diagnostics.OutputTokenCount,
                    responseCharacterCount = diagnostics.ResponseCharacterCount
                };
        }

        return AppException.ServiceUnavailable(
            "The AI assistant provider is currently unavailable.",
            "ASSISTANT_PROVIDER_UNAVAILABLE",
            inner,
            details);
    }

    private static void LogInvalidJson(ILogger? logger, AssistantProviderDiagnostics diagnostics, Exception? inner)
    {
        if (logger is null)
            return;

        logger.LogWarning(
            inner,
            "Assistant JSON parse failed. Stage={Stage} Model={Model} FinishReason={FinishReason} ConfiguredMaxOutputTokens={ConfiguredMaxOutputTokens} OutputTokenCount={OutputTokenCount} ResponseCharacterCount={ResponseCharacterCount} ParseFailureCategory={ParseFailureCategory} RequestId={RequestId} JsonPrefix={JsonPrefix}",
            diagnostics.Stage,
            diagnostics.Model,
            diagnostics.FinishReason,
            diagnostics.ConfiguredMaxOutputTokens,
            diagnostics.OutputTokenCount,
            diagnostics.ResponseCharacterCount,
            diagnostics.ParseFailureCategory,
            diagnostics.RequestId,
            diagnostics.JsonPrefix);
    }

    public static AppException MarketNotFound()
        => AppException.NotFound("Night market was not found.", "ASSISTANT_MARKET_NOT_FOUND");

    public static AppException MealPlanNotFound()
        => AppException.NotFound("Meal plan was not found.", "ASSISTANT_MEAL_PLAN_NOT_FOUND");

    public static AppException MealPlanEmpty()
        => AppException.UnprocessableEntity("Meal plan has no items to add.", "ASSISTANT_MEAL_PLAN_EMPTY");

    public static AppException MealPlanUnavailable()
        => AppException.Conflict(
            "A meal plan item is no longer available to add to cart.",
            "ASSISTANT_MEAL_PLAN_UNAVAILABLE");

    public static AppException MealPlanPriceChanged()
        => AppException.Conflict(
            "A meal plan item price or promotion changed. Refresh the plan and try again.",
            "ASSISTANT_MEAL_PLAN_PRICE_CHANGED");

    public static AppException MealPlanItemNotFound()
        => AppException.NotFound("Meal plan item was not found.", "ASSISTANT_MEAL_PLAN_ITEM_NOT_FOUND");

    public static AppException PlanChanged()
        => AppException.Conflict(
            "The meal plan or replacement pool has changed. Refresh the plan and try again.",
            "PLAN_CHANGED");
}
