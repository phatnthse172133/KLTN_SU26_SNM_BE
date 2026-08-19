namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    public int CandidateBatchSize { get; set; } = 8;
    /// <summary>
    /// Extra Stage B batch attempts when the provider returns a score count mismatch (0..1).
    /// Does not repeat Stage A or completed batches.
    /// </summary>
    public int SemanticBatchRetryCount { get; set; } = 1;
    public int SemanticBatchMaxConcurrency { get; set; } = 4;
    public int MaxRecommendations { get; set; } = 8;
    public int MaxMealPlanOptions { get; set; } = 3;
    public int MaxMealPlanCandidates { get; set; } = 40;
    public int MaxMealPlanItemsPerPlan { get; set; } = 8;
    public double MinimumCompatibilityScore { get; set; } = 0.4;
    /// <summary>
    /// Stage B semantic score floor (0..1). Candidates below this are excluded before ranking.
    /// Stage B typically scores clearly relevant items ~0.6–0.95 and unrelated items ~0.0–0.45.
    /// </summary>
    public double MinimumSemanticRelevanceScore { get; set; } = 0.55;
    public double SemanticWeight { get; set; } = 0.40;
    public double StructuredPreferenceWeight { get; set; } = 0.20;
    public double PriceWeight { get; set; } = 0.15;
    public double RatingWeight { get; set; } = 0.10;
    public double DistanceWeight { get; set; } = 0.08;
    public double FeaturedWeight { get; set; } = 0.03;
    public double OpenNowWeight { get; set; } = 0.02;
    public double PromoWeight { get; set; } = 0.02;
    public int ConversationHistoryLimit { get; set; } = 8;
    public bool TreatMayContainAsHard { get; set; } = true;
    public int DefaultMaxDistanceMeters { get; set; } = 5000;
    public int MaxMessageLength { get; set; } = 2000;
    public CompactCandidateProjectionOptions CompactCandidateProjection { get; set; } = new();
}

public sealed class CompactCandidateProjectionOptions
{
    public bool IncludeDescription { get; set; } = true;
    public bool IncludeIngredients { get; set; } = true;
    public bool IncludeAllergens { get; set; } = true;
    public bool IncludeDietary { get; set; } = true;
    public bool IncludeTastes { get; set; } = true;
    public bool IncludePreparation { get; set; } = true;
    public bool IncludeCourses { get; set; } = true;
    public int MaxDescriptionCharacters { get; set; } = 280;
}
