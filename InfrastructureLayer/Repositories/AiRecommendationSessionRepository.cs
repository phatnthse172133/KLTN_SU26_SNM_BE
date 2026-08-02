using ApplicationLayer.AI.V2.Services;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public sealed class AiRecommendationSessionRepository(SNMDbContext db) : IAiRecommendationSessionRepository
{
    public async Task SaveSessionAsync(AiRecommendationSession session, IReadOnlyCollection<AiRecommendationResult> results, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        db.AiRecommendationSessions.Add(session);
        db.AiRecommendationResults.AddRange(results);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task<RecommendationFeedbackRecordResult> RecordFeedbackAsync(Guid customerId, Guid sessionId, Guid foodId,
        AiRecommendationFeedbackAction action, DateTime utcNow, int feedbackWindowMinutes, CancellationToken cancellationToken)
    {
        var session = await db.AiRecommendationSessions.SingleOrDefaultAsync(value => value.Id == sessionId && value.CustomerId == customerId, cancellationToken);
        if (session is null) return new(RecommendationFeedbackRecordStatus.NOT_FOUND, null);
        var feedbackCutoff = session.CreatedAt.AddMinutes(Math.Clamp(feedbackWindowMinutes, 1, 1440));
        if (session.IsExpired(utcNow) || utcNow >= feedbackCutoff) return new(RecommendationFeedbackRecordStatus.EXPIRED, null);
        if (!await db.AiRecommendationResults.AnyAsync(value => value.SessionId == sessionId && value.FoodItemId == foodId, cancellationToken))
            return new(RecommendationFeedbackRecordStatus.FOOD_NOT_IN_SESSION, null);
        AiRecommendationFeedback feedback;
        if (action is AiRecommendationFeedbackAction.LIKED or AiRecommendationFeedbackAction.DISLIKED)
        {
            if (db.Database.IsRelational())
            {
                var feedbackId = Guid.NewGuid();
                var actionCode = action.ToString();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "AiRecommendationFeedback" ("Id", "SessionId", "FoodItemId", "Action", "CreatedAt")
                    VALUES ({feedbackId}, {sessionId}, {foodId}, {actionCode}, {utcNow})
                    ON CONFLICT ("SessionId", "FoodItemId")
                    WHERE "FoodItemId" IS NOT NULL AND "Action" IN ('LIKED', 'DISLIKED')
                    DO UPDATE SET "Action" = EXCLUDED."Action", "CreatedAt" = EXCLUDED."CreatedAt"
                    WHERE EXCLUDED."CreatedAt" >= "AiRecommendationFeedback"."CreatedAt";
                    """, cancellationToken);
                feedback = await db.AiRecommendationFeedback.AsNoTracking().SingleAsync(value => value.SessionId == sessionId
                    && value.FoodItemId == foodId && (value.Action == AiRecommendationFeedbackAction.LIKED
                        || value.Action == AiRecommendationFeedbackAction.DISLIKED), cancellationToken);
                return new(RecommendationFeedbackRecordStatus.RECORDED, feedback);
            }
            feedback = await db.AiRecommendationFeedback.SingleOrDefaultAsync(value => value.SessionId == sessionId && value.FoodItemId == foodId
                && (value.Action == AiRecommendationFeedbackAction.LIKED || value.Action == AiRecommendationFeedbackAction.DISLIKED), cancellationToken)
                ?? new AiRecommendationFeedback { Id = Guid.NewGuid(), SessionId = sessionId, FoodItemId = foodId };
            feedback.Action = action; feedback.CreatedAt = utcNow;
            if (db.Entry(feedback).State == EntityState.Detached) db.AiRecommendationFeedback.Add(feedback);
        }
        else
        {
            feedback = new AiRecommendationFeedback { Id = Guid.NewGuid(), SessionId = sessionId, FoodItemId = foodId, Action = action, CreatedAt = utcNow };
            db.AiRecommendationFeedback.Add(feedback);
        }
        await db.SaveChangesAsync(cancellationToken);
        return new(RecommendationFeedbackRecordStatus.RECORDED, feedback);
    }

    public async Task<int> DeleteExpiredBatchAsync(DateTime retentionCutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        batchSize = Math.Clamp(batchSize, 1, 1000);
        var ids = await db.AiRecommendationSessions.AsNoTracking().Where(value => value.ExpiresAt < retentionCutoffUtc)
            .OrderBy(value => value.ExpiresAt).ThenBy(value => value.Id).Select(value => value.Id).Take(batchSize).ToListAsync(cancellationToken);
        if (ids.Count == 0) return 0;
        return await db.AiRecommendationSessions.Where(value => ids.Contains(value.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}
