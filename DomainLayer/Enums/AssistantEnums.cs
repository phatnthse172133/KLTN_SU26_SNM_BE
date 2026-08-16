namespace DomainLayer.Enums;

public enum AssistantConversationStatus
{
    Active,
    LocationPending
}

public enum AssistantMessageRole
{
    User,
    Assistant
}

public enum AssistantIntentKind
{
    FOOD_RECOMMENDATION,
    MEAL_PLAN,
    CHITCHAT,
    CLARIFY
}
