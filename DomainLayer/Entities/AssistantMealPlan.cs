namespace DomainLayer.Entities;

public sealed class AssistantMealPlan
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid NightMarketId { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal? BudgetMax { get; set; }
    public int PartySize { get; set; }
    public DateTime CreatedAt { get; set; }

    public AssistantConversation Conversation { get; set; } = null!;
    public NightMarket NightMarket { get; set; } = null!;
    public ICollection<AssistantMealPlanItem> Items { get; set; } = new List<AssistantMealPlanItem>();
}
