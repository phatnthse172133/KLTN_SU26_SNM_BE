using DomainLayer.Enums;

namespace DomainLayer.Entities;

public sealed class AiRecommendationSession
{
    public Guid Id { get; set; }
    public Guid? CustomerId { get; set; }
    public string OriginalQuery { get; set; } = null!;
    public string? ParsedPreferenceJson { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? MaxDistanceMeters { get; set; }
    public AiSessionStatus Status { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderModelName { get; set; }
    public string? ProviderRequestId { get; set; }
    public string? ProviderFailureCategory { get; set; }
    public bool UsedFallback { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public User? Customer { get; set; }
    public ICollection<AiRecommendationFeedback> Feedback { get; set; } = new List<AiRecommendationFeedback>();
    public ICollection<AiRecommendationResult> Results { get; set; } = new List<AiRecommendationResult>();
    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;
}

public sealed class AiRecommendationResult
{
    public Guid SessionId { get; set; }
    public Guid FoodItemId { get; set; }
    public int Rank { get; set; }
    public decimal Score { get; set; }
    public RecommendationMatchTier MatchTier { get; set; }
    public DateTime CreatedAt { get; set; }
    public AiRecommendationSession Session { get; set; } = null!;
    public FoodItem FoodItem { get; set; } = null!;
}

public sealed class AiRecommendationFeedback
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? FoodItemId { get; set; }
    public AiRecommendationFeedbackAction Action { get; set; }
    public DateTime CreatedAt { get; set; }
    public AiRecommendationSession Session { get; set; } = null!;
    public FoodItem? FoodItem { get; set; }
}

public sealed class AiMealPlanSession
{
    public Guid Id { get; set; }
    public Guid? CustomerId { get; set; }
    public int PartySize { get; set; }
    public decimal Budget { get; set; }
    public string DiningStyle { get; set; } = null!;
    public string? OriginalRequest { get; set; }
    public string? ParsedPreferenceJson { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? MaxDistanceMeters { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? RequestHash { get; set; }
    public bool UsedProviderFallback { get; set; }
    public string? WarningsJson { get; set; }
    public AiSessionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public User? Customer { get; set; }
    public ICollection<AiMealPlan> Plans { get; set; } = new List<AiMealPlan>();

    public static AiMealPlanSession Create(int partySize, decimal budget, DateTime createdAt, DateTime expiresAt)
    {
        if (partySize <= 0) throw new ArgumentOutOfRangeException(nameof(partySize));
        if (budget <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
        if (expiresAt <= createdAt) throw new ArgumentException("Expiration must be after creation.", nameof(expiresAt));
        return new AiMealPlanSession { Id = Guid.NewGuid(), PartySize = partySize, Budget = budget, CreatedAt = createdAt, ExpiresAt = expiresAt };
    }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;
}

public sealed class AiMealPlan
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid MarketId { get; set; }
    public string PlanCode { get; set; } = null!;
    public string PlanTitle { get; set; } = null!;
    public string Strategy { get; set; } = null!;
    public string? Summary { get; set; }
    public string? WarningsJson { get; set; }
    public decimal TotalPrice { get; private set; }
    public decimal RemainingBudget { get; private set; }
    public int? DistanceMeters { get; set; }
    public int? EstimatedTravelMinutes { get; set; }
    public int? EstimatedServingCount { get; private set; }
    private decimal _compatibilityScore;
    public decimal CompatibilityScore
    {
        get => _compatibilityScore;
        set
        {
            if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value));
            _compatibilityScore = value;
        }
    }
    public bool IsComplete { get; private set; }
    public int Version { get; private set; }
    public AiMealPlanStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public AiMealPlanSession Session { get; set; } = null!;
    public NightMarket Market { get; set; } = null!;
    public ICollection<AiMealPlanItem> Items { get; set; } = new List<AiMealPlanItem>();

    public void AddItem(AiMealPlanItem item, Guid boothMarketId, decimal budget, bool recalculate = true)
    {
        if (boothMarketId != MarketId) throw new InvalidOperationException("All plan items must belong to the plan market.");
        if (item.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(item.Quantity));
        if (item.UnitPriceSnapshot < 0) throw new ArgumentOutOfRangeException(nameof(item.UnitPriceSnapshot));
        if (item.CompatibilityScore is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(item.CompatibilityScore));
        if (Items.Any(existing => !existing.IsRemoved && existing.FoodItemId == item.FoodItemId))
            throw new InvalidOperationException("An active food item cannot be duplicated in a plan.");
        item.TotalPriceSnapshot = item.UnitPriceSnapshot * item.Quantity;
        if (Items.Where(existing => !existing.IsRemoved).Sum(existing => existing.TotalPriceSnapshot) + item.TotalPriceSnapshot > budget)
            throw new InvalidOperationException("The item would exceed the plan budget.");
        Items.Add(item);
        if (recalculate) Recalculate(budget);
    }

    public void RemoveItem(Guid itemId, decimal budget)
    {
        var item = Items.Single(value => value.Id == itemId);
        if (!item.IsRemoved) { item.MarkRemoved(DateTime.UtcNow); Recalculate(budget); }
    }

    public void ReplaceItem(Guid itemId, AiMealPlanItem replacement, Guid boothMarketId, decimal budget)
    {
        var current = Items.Single(value => value.Id == itemId && !value.IsRemoved);
        current.MarkRemoved(DateTime.UtcNow);
        AddItem(replacement, boothMarketId, budget, false);
    }

    public void MarkComplete(int requiredActiveItems = 1)
    {
        if (Items.Count(item => !item.IsRemoved) < requiredActiveItems)
            throw new InvalidOperationException("The plan does not have the required structure.");
        IsComplete = true;
        Version++;
    }

    private void Recalculate(decimal budget)
    {
        if (budget <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
        var active = Items.Where(item => !item.IsRemoved).ToArray();
        TotalPrice = active.Sum(item => item.TotalPriceSnapshot);
        RemainingBudget = budget - TotalPrice;
        EstimatedServingCount = active.Any(item => item.ServingCountSnapshot.HasValue)
            ? active.Sum(item => item.ServingCountSnapshot ?? 0) : null;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyAuthoritativeCalculation(decimal budget, int? servingCount, bool isComplete,
        decimal compatibilityScore, string? warningsJson, DateTime utcNow, bool incrementVersion = true)
    {
        if (budget <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
        var active = Items.Where(item => !item.IsRemoved).ToArray();
        var total = active.Sum(item => item.UnitPriceSnapshot * item.Quantity);
        if (total > budget) throw new InvalidOperationException("The plan exceeds its budget.");
        foreach (var item in active) item.TotalPriceSnapshot = item.UnitPriceSnapshot * item.Quantity;
        TotalPrice = total;
        RemainingBudget = budget - total;
        EstimatedServingCount = servingCount;
        IsComplete = isComplete;
        CompatibilityScore = Math.Round(Math.Clamp(compatibilityScore, 0, 100), 2, MidpointRounding.AwayFromZero);
        WarningsJson = warningsJson;
        UpdatedAt = utcNow;
        if (incrementVersion) Version++;
    }
}

public sealed class AiMealPlanItem
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid? FoodItemId { get; set; }
    public Guid? BoothId { get; set; }
    public string FoodNameSnapshot { get; set; } = null!;
    public string BoothNameSnapshot { get; set; } = null!;
    public string? ImageUrlSnapshot { get; set; }
    public FoodCourse Course { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public decimal TotalPriceSnapshot { get; set; }
    public int? ServingCountSnapshot { get; set; }
    public decimal? RatingSnapshot { get; set; }
    public int ReviewCountSnapshot { get; set; }
    private decimal _compatibilityScore;
    public decimal CompatibilityScore
    {
        get => _compatibilityScore;
        set
        {
            if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value));
            _compatibilityScore = value;
        }
    }
    public string Reason { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsRemoved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public AiMealPlan Plan { get; set; } = null!;
    public FoodItem? FoodItem { get; set; }
    public Booth? Booth { get; set; }

    public void MarkRemoved(DateTime utcNow)
    {
        IsRemoved = true;
        UpdatedAt = utcNow;
    }
}

public sealed class FoodAiProfile
{
    public Guid FoodItemId { get; set; }
    public string SearchText { get; set; } = null!;
    public float[]? Embedding { get; set; }
    public string? EmbeddingModel { get; set; }
    public string ContentHash { get; set; } = null!;
    public FoodAiProfileStatus Status { get; set; }
    public int Version { get; set; }
    public DateTime? EmbeddedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
}
