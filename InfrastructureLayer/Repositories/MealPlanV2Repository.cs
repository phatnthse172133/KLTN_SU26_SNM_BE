using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.AI.V2.Services;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace InfrastructureLayer.Repositories;

public sealed class MealPlanCandidateRepository(IFoodRecommendationReadRepository foods) : IMealPlanCandidateRepository
{
    public async Task<IReadOnlyCollection<FoodRecommendationCandidate>> GetCandidatesAsync(DateTime utcNow, int limit,
        int maximumPerMarket, CancellationToken cancellationToken)
    {
        var loaded = await foods.GetCandidatesAsync(utcNow, Math.Clamp(limit, 1, 500), cancellationToken);
        return loaded.GroupBy(value => value.MarketId).OrderBy(group => group.Key)
            .SelectMany(group => group.Take(Math.Clamp(maximumPerMarket, 1, 100))).ToArray();
    }
}

public sealed class MealPlanV2Repository(SNMDbContext db) : IMealPlanV2Repository
{
    public Task<AiMealPlanSession?> FindSessionAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken)
        => SessionQuery(false).SingleOrDefaultAsync(value => value.CustomerId == customerId && value.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<MealPlanIdempotencyResult> SaveCreateAsync(AiMealPlanSession session, CancellationToken cancellationToken)
    {
        var existing = await FindSessionAsync(session.CustomerId!.Value, session.IdempotencyKey!, cancellationToken);
        if (existing is not null)
            return new(existing.RequestHash == session.RequestHash ? MealPlanIdempotencyStatus.EXISTING : MealPlanIdempotencyStatus.CONFLICT, existing);
        db.AiMealPlanSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(MealPlanIdempotencyStatus.CREATED, session);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            existing = await FindSessionAsync(session.CustomerId.Value, session.IdempotencyKey!, cancellationToken);
            if (existing is null)
                throw new InvalidOperationException("The idempotent meal-plan create conflicted but the winning row was not visible.", exception);
            return new(existing.RequestHash == session.RequestHash ? MealPlanIdempotencyStatus.EXISTING : MealPlanIdempotencyStatus.CONFLICT, existing);
        }
    }

    public Task<AiMealPlan?> GetOwnedPlanAsync(Guid customerId, Guid planId, CancellationToken cancellationToken)
        => PlanQuery(false).SingleOrDefaultAsync(value => value.Id == planId && value.Session.CustomerId == customerId, cancellationToken);

    public async Task<IMealPlanMutation?> BeginOwnedMutationAsync(Guid customerId, Guid planId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var plan = await db.AiMealPlans
                .FromSqlInterpolated($"SELECT * FROM \"AiMealPlan\" WHERE \"Id\" = {planId} FOR UPDATE")
                .Include(value => value.Session).Include(value => value.Market)
                .Include(value => value.Items).ThenInclude(value => value.FoodItem).ThenInclude(value => value!.FoodPrices)
                .Include(value => value.Items).ThenInclude(value => value.FoodItem).ThenInclude(value => value!.Booth).ThenInclude(value => value.NightMarket)
                .Include(value => value.Items).ThenInclude(value => value.Booth)
                .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
            if (plan is null || plan.Session.CustomerId != customerId)
            {
                await transaction.RollbackAsync(cancellationToken);
                await transaction.DisposeAsync();
                return null;
            }
            return new Mutation(db, transaction, plan);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<int> DeleteExpiredBatchAsync(DateTime retentionCutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await db.AiMealPlanSessions.Where(value => value.ExpiresAt < retentionCutoffUtc)
            .OrderBy(value => value.ExpiresAt).Select(value => value.Id).Take(Math.Clamp(batchSize, 1, 500)).ToArrayAsync(cancellationToken);
        return ids.Length == 0 ? 0 : await db.AiMealPlanSessions.Where(value => ids.Contains(value.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    private IQueryable<AiMealPlanSession> SessionQuery(bool tracking)
    {
        var query = db.AiMealPlanSessions.Include(value => value.Plans).ThenInclude(value => value.Market)
            .Include(value => value.Plans).ThenInclude(value => value.Items).AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private IQueryable<AiMealPlan> PlanQuery(bool tracking)
    {
        var query = db.AiMealPlans.Include(value => value.Session).Include(value => value.Market)
            .Include(value => value.Items).ThenInclude(value => value.FoodItem).ThenInclude(value => value!.FoodPrices)
            .Include(value => value.Items).ThenInclude(value => value.FoodItem).ThenInclude(value => value!.Booth).ThenInclude(value => value.NightMarket)
            .Include(value => value.Items).ThenInclude(value => value.Booth).AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private sealed class Mutation(SNMDbContext db, IDbContextTransaction transaction, AiMealPlan plan) : IMealPlanMutation
    {
        private bool _committed;
        public AiMealPlan Plan { get; } = plan;
        public Task<AiMealPlanCartOperation?> FindCartOperationAsync(Guid customerId, string idempotencyKey,
            CancellationToken cancellationToken)
            => db.AiMealPlanCartOperations.SingleOrDefaultAsync(
                value => value.CustomerId == customerId && value.IdempotencyKey == idempotencyKey,
                cancellationToken);
        public void AddCartOperation(AiMealPlanCartOperation operation)
            => db.AiMealPlanCartOperations.Add(operation);
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            foreach (var item in Plan.Items)
            {
                item.PlanId = Plan.Id;
                if (db.Entry(item).State == EntityState.Detached)
                    db.AiMealPlanItems.Add(item);
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }
        public async ValueTask DisposeAsync()
        {
            if (!_committed) await transaction.RollbackAsync();
            await transaction.DisposeAsync();
        }
    }
}
