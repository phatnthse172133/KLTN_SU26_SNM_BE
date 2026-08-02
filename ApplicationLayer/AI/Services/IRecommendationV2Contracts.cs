using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace ApplicationLayer.AI.V2.Services;

public interface IFoodRecommendationReadRepository
{
    Task<IReadOnlyCollection<FoodRecommendationCandidate>> GetCandidatesAsync(DateTime utcNow, int limit, CancellationToken cancellationToken);
}

public interface IAiRecommendationSessionRepository
{
    Task SaveSessionAsync(AiRecommendationSession session, IReadOnlyCollection<AiRecommendationResult> results, CancellationToken cancellationToken);
    Task<RecommendationFeedbackRecordResult> RecordFeedbackAsync(Guid customerId, Guid sessionId, Guid foodId, AiRecommendationFeedbackAction action,
        DateTime utcNow, int feedbackWindowMinutes, CancellationToken cancellationToken);
    Task<int> DeleteExpiredBatchAsync(DateTime retentionCutoffUtc, int batchSize, CancellationToken cancellationToken);
}

public enum RecommendationFeedbackRecordStatus { RECORDED, NOT_FOUND, EXPIRED, FOOD_NOT_IN_SESSION }
public sealed record RecommendationFeedbackRecordResult(RecommendationFeedbackRecordStatus Status, AiRecommendationFeedback? Feedback);

public interface IFoodSemanticMatcher { SemanticMatchResult Match(FoodRecommendationIntent intent, FoodRecommendationCandidate candidate); }
public interface IFoodRecommendationRanker
{
    RankedRecommendationCandidate Rank(FoodRecommendationIntent intent, FoodRecommendationCandidate candidate, SemanticMatchResult semantic, int? distanceMeters);
}
public interface IFoodRecommendationDiversityReranker
{
    IReadOnlyCollection<RankedRecommendationCandidate> Rerank(IReadOnlyCollection<RankedRecommendationCandidate> candidates, FoodRecommendationSortPreference sortPreference);
}
public interface IFoodRecommendationV2Service
{
    Task<ApiResponse<FoodRecommendationV2Response>> RecommendAsync(Guid customerId, CreateFoodRecommendationV2Request request, CancellationToken cancellationToken);
    Task<ApiResponse<RecommendationFeedbackV2Response>> FeedbackAsync(Guid customerId, Guid sessionId, RecommendationFeedbackV2Request request, CancellationToken cancellationToken);
}
