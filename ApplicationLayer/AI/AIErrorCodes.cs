namespace ApplicationLayer.AI;

public static class AIErrorCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string NoCandidates = "NO_CANDIDATES";
    public const string NoPlanFound = "NO_PLAN_FOUND";
    public const string PlanLogUnavailable = "PLAN_LOG_UNAVAILABLE";
    public const string PlanChanged = "PLAN_CHANGED";
    public const string ItemUnavailable = "ITEM_UNAVAILABLE";
    public const string BoothNotOrderable = "BOOTH_NOT_ORDERABLE";
    public const string MarketNotOrderable = "MARKET_NOT_ORDERABLE";
    public const string PriceChanged = "PRICE_CHANGED";
    public const string BudgetExceeded = "BUDGET_EXCEEDED";
}
