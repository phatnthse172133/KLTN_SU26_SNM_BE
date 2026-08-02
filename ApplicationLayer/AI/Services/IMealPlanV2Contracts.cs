using ApplicationLayer.AI.V2.MealPlans;
using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.Enums;
using ApplicationLayer.DTOs.Responses;

namespace ApplicationLayer.AI.V2.Services;

public interface IMealPlanCandidateRepository
{
    Task<IReadOnlyCollection<FoodRecommendationCandidate>> GetCandidatesAsync(DateTime utcNow, int limit,
        int maximumPerMarket, CancellationToken cancellationToken);
}

public enum MealPlanIdempotencyStatus { CREATED, EXISTING, CONFLICT }
public sealed record MealPlanIdempotencyResult(MealPlanIdempotencyStatus Status, AiMealPlanSession Session);

public interface IMealPlanMutation : IAsyncDisposable
{
    AiMealPlan Plan { get; }
    Task<AiMealPlanCartOperation?> FindCartOperationAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);
    void AddCartOperation(AiMealPlanCartOperation operation);
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IMealPlanCartIntegrationService
{
    Task<CartBatchAddResponse> AddItemsAsync(Guid customerId, IReadOnlyCollection<(Guid FoodItemId, int Quantity)> items,
        CancellationToken cancellationToken);
}

public interface IMealPlanV2Repository
{
    Task<AiMealPlanSession?> FindSessionAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);
    Task<MealPlanIdempotencyResult> SaveCreateAsync(AiMealPlanSession session, CancellationToken cancellationToken);
    Task<AiMealPlan?> GetOwnedPlanAsync(Guid customerId, Guid planId, CancellationToken cancellationToken);
    Task<IMealPlanMutation?> BeginOwnedMutationAsync(Guid customerId, Guid planId, CancellationToken cancellationToken);
    Task<int> DeleteExpiredBatchAsync(DateTime retentionCutoffUtc, int batchSize, CancellationToken cancellationToken);
}

public interface IMealPlanPolicyResolver { MealPlanStylePolicy Resolve(MealPlanDiningStyle style); }
public interface IMealPlanRecalculationService
{
    MealPlanScoreBreakdown Recalculate(AiMealPlan plan, MealPlanStylePolicy policy, int partySize,
        decimal budget, DateTime utcNow, bool incrementVersion = true);
}

public interface IMealPlanV2Service
{
    Task<ApiResponse<MealPlanV2Response>> CreateAsync(Guid customerId, CreateMealPlanV2Request request, CancellationToken cancellationToken);
    Task<ApiResponse<MealPlanDetailResponse>> GetDetailAsync(Guid customerId, Guid planId, CancellationToken cancellationToken);
    Task<ApiResponse<MealPlanAlternativePageResponse>> GetAlternativesAsync(Guid customerId, Guid planId, Guid itemId,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<ApiResponse<MealPlanDetailResponse>> ReplaceAsync(Guid customerId, Guid planId, Guid itemId,
        ReplaceMealPlanItemRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<MealPlanDetailResponse>> RemoveAsync(Guid customerId, Guid planId, Guid itemId,
        int expectedPlanVersion, CancellationToken cancellationToken);
    Task<ApiResponse<MealPlanDetailResponse>> RegenerateCourseAsync(Guid customerId, Guid planId, FoodCourse course,
        RegenerateMealPlanCourseRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<MealPlanAddToCartResponse>> AddToCartAsync(Guid customerId, Guid planId,
        AddMealPlanToCartRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<MealPlanDetailResponse>> RefreshPricesAsync(Guid customerId, Guid planId,
        RefreshMealPlanPricesRequest request, CancellationToken cancellationToken);
}
