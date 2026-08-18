using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfrastructureLayer.Repositories;

public sealed class AssistantConversationRepository(SNMDbContext db) : IAssistantConversationRepository
{
    public Task<AssistantConversation?> GetOwnedAsync(Guid conversationId, Guid customerId, CancellationToken cancellationToken = default)
        => db.AssistantConversations
            .AsTracking()
            .FirstOrDefaultAsync(item => item.Id == conversationId && item.CustomerId == customerId, cancellationToken);

    public async Task<IReadOnlyList<AssistantMessage>> GetRecentMessagesAsync(Guid conversationId, int limit, CancellationToken cancellationToken = default)
    {
        var take = Math.Max(1, limit);
        var items = await db.AssistantMessages
            .AsNoTracking()
            .Where(item => item.ConversationId == conversationId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
        items.Reverse();
        return items;
    }

    public Task<AssistantMealPlan?> GetOwnedMealPlanAsync(Guid conversationId, Guid mealPlanId, Guid customerId, CancellationToken cancellationToken = default)
        => db.AssistantMealPlans
            .Include(item => item.NightMarket)
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.Id == mealPlanId && item.ConversationId == conversationId && item.CustomerId == customerId,
                cancellationToken);

    public void Add(AssistantConversation conversation) => db.AssistantConversations.Add(conversation);

    public void AddMessage(AssistantMessage message) => db.AssistantMessages.Add(message);

    public void AddMealPlan(AssistantMealPlan plan) => db.AssistantMealPlans.Add(plan);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    // One extra SaveChanges on the same uncommitted unit of work. OpenAI/semantic
    // already ran before this method; this retry never re-enters them.
    public const int PersistConcurrencyRetries = 1;

    public async Task SaveTurnAsync(AssistantConversation conversation, CancellationToken cancellationToken = default)
    {
        EnsureTrackedHeader(conversation);
        var remaining = PersistConcurrencyRetries;
        while (true)
        {
            IDbContextTransaction? transaction = null;
            try
            {
                if (db.Database.IsRelational())
                    transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (remaining > 0)
            {
                remaining--;
                if (transaction is not null)
                    await transaction.RollbackAsync(cancellationToken);
                if (!await TryRestoreHeaderAfterConcurrencyAsync(conversation, cancellationToken))
                    throw;
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                if (transaction is not null)
                    await transaction.DisposeAsync();
            }
        }
    }

    private void EnsureTrackedHeader(AssistantConversation conversation)
    {
        var header = db.Entry(conversation);
        if (header.State is EntityState.Detached)
            throw new InvalidOperationException("AssistantConversation must remain tracked for SaveTurnAsync.");
        if (header.State is not (EntityState.Unchanged or EntityState.Modified))
            throw new InvalidOperationException($"AssistantConversation is in unexpected state {header.State}.");
    }

    private async Task<bool> TryRestoreHeaderAfterConcurrencyAsync(
        AssistantConversation conversation,
        CancellationToken cancellationToken)
    {
        var header = db.Entry(conversation);
        if (header.State is EntityState.Detached)
            return false;

        var status = conversation.Status;
        var updatedAt = conversation.UpdatedAt;
        var pendingMessage = conversation.PendingUserMessage;
        var pendingIntent = conversation.PendingParsedIntentJson;
        await header.ReloadAsync(cancellationToken);
        if (header.State is EntityState.Detached)
            return false;

        conversation.Status = status;
        conversation.UpdatedAt = updatedAt;
        conversation.PendingUserMessage = pendingMessage;
        conversation.PendingParsedIntentJson = pendingIntent;
        EnsureTrackedHeader(conversation);
        return true;
    }
}
