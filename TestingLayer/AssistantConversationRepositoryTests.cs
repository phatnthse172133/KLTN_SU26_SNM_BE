using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;

namespace TestingLayer;

public sealed class AssistantConversationRepositoryTests
{
    [Fact]
    public async Task GetRecentMessages_SameCreatedAt_OrdersByIdAsTieBreaker()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new SNMDbContext(options);

        var conversationId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        var smallerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var largerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        db.AssistantConversations.Add(new AssistantConversation
        {
            Id = conversationId,
            CustomerId = Guid.NewGuid(),
            Status = AssistantConversationStatus.Active,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });
        db.AssistantMessages.AddRange(
            new AssistantMessage
            {
                Id = largerId,
                ConversationId = conversationId,
                Role = AssistantMessageRole.Assistant,
                Content = "later-by-id",
                CreatedAt = createdAt
            },
            new AssistantMessage
            {
                Id = smallerId,
                ConversationId = conversationId,
                Role = AssistantMessageRole.User,
                Content = "earlier-by-id",
                CreatedAt = createdAt
            });
        await db.SaveChangesAsync();

        var history = await new AssistantConversationRepository(db).GetRecentMessagesAsync(conversationId, 10);

        Assert.Equal(2, history.Count);
        Assert.Equal(new[] { smallerId, largerId }, history.Select(item => item.Id).ToArray());
        Assert.True(history[0].CreatedAt == history[1].CreatedAt);
        Assert.True(history[0].Id.CompareTo(history[1].Id) < 0);
    }
}
