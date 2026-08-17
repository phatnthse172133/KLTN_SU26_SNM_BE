using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IAssistantConversationRepository
{
    Task<AssistantConversation?> GetOwnedAsync(Guid conversationId, Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssistantMessage>> GetRecentMessagesAsync(Guid conversationId, int limit, CancellationToken cancellationToken = default);
    Task<AssistantMealPlan?> GetOwnedMealPlanAsync(Guid conversationId, Guid mealPlanId, Guid customerId, CancellationToken cancellationToken = default);
    void Add(AssistantConversation conversation);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
