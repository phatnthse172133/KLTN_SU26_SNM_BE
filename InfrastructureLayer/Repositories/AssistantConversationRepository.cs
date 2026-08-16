using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public sealed class AssistantConversationRepository(SNMDbContext db) : IAssistantConversationRepository
{
    public Task<AssistantConversation?> GetOwnedAsync(Guid conversationId, Guid customerId, CancellationToken cancellationToken = default)
        => db.AssistantConversations
            .Include(item => item.Messages)
            .Include(item => item.MealPlans).ThenInclude(item => item.Items)
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
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.Id == mealPlanId && item.ConversationId == conversationId && item.CustomerId == customerId,
                cancellationToken);

    public void Add(AssistantConversation conversation) => db.AssistantConversations.Add(conversation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
