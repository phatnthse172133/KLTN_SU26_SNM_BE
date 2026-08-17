using DomainLayer.Enums;

namespace DomainLayer.Entities;

public sealed class AssistantConversation
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? MarketId { get; set; }
    public AssistantConversationStatus Status { get; set; } = AssistantConversationStatus.Active;
    public string? PendingUserMessage { get; set; }
    public string? PendingParsedIntentJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Customer { get; set; } = null!;
    public NightMarket? Market { get; set; }
    public ICollection<AssistantMessage> Messages { get; set; } = new List<AssistantMessage>();
    public ICollection<AssistantMealPlan> MealPlans { get; set; } = new List<AssistantMealPlan>();
}
