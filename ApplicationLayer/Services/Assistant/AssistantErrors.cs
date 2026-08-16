using ApplicationLayer.Exceptions;

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

    public static AppException ProviderUnavailable(Exception? inner = null)
        => AppException.ServiceUnavailable(
            "The AI assistant provider is currently unavailable.",
            "ASSISTANT_PROVIDER_UNAVAILABLE",
            inner);

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
}
