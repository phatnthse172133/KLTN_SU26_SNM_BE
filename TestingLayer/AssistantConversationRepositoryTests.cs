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

    [Fact]
    public async Task GetOwned_DoesNotMarkCollectionsLoaded()
    {
        var (db, conversationId, customerId) = await SeedConversationAsync();
        await using (db)
        {
            db.ChangeTracker.Clear();
            var loaded = await new AssistantConversationRepository(db).GetOwnedAsync(conversationId, customerId);
            Assert.NotNull(loaded);
            var entry = db.Entry(loaded!);
            Assert.False(entry.Collection(item => item.Messages).IsLoaded);
            Assert.False(entry.Collection(item => item.MealPlans).IsLoaded);
        }
    }

    [Fact]
    public async Task SaveTurn_PersistsNewMessagesAndHeader()
    {
        var (db, conversationId, customerId) = await SeedConversationAsync();
        await using (db)
        {
            db.ChangeTracker.Clear();
            var repo = new AssistantConversationRepository(db);
            var loaded = await repo.GetOwnedAsync(conversationId, customerId);
            Assert.NotNull(loaded);
            var now = new DateTime(2026, 8, 18, 13, 0, 0, DateTimeKind.Utc);
            repo.AddMessage(new AssistantMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = loaded.Id,
                Role = AssistantMessageRole.User,
                Content = "hi",
                CreatedAt = now
            });
            repo.AddMessage(new AssistantMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = loaded.Id,
                Role = AssistantMessageRole.Assistant,
                Content = "hello",
                CreatedAt = now
            });
            loaded.UpdatedAt = now;
            await repo.SaveTurnAsync(loaded);

            Assert.Equal(2, await db.AssistantMessages.CountAsync(item => item.ConversationId == conversationId));
            var header = await db.AssistantConversations.AsNoTracking().SingleAsync(item => item.Id == conversationId);
            Assert.Equal(now, header.UpdatedAt);
        }
    }

    [Fact]
    public async Task SaveTurn_MissingHeader_ThrowsConcurrency()
    {
        var name = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        var conversationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        await using (var seed = new SNMDbContext(options))
        {
            seed.AssistantConversations.Add(new AssistantConversation
            {
                Id = conversationId,
                CustomerId = customerId,
                Status = AssistantConversationStatus.Active,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
            await seed.SaveChangesAsync();
        }

        await using var db = new SNMDbContext(options);
        var repo = new AssistantConversationRepository(db);
        var loaded = await repo.GetOwnedAsync(conversationId, customerId);
        Assert.NotNull(loaded);
        repo.AddMessage(new AssistantMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = AssistantMessageRole.User,
            Content = "hi",
            CreatedAt = DateTime.UtcNow
        });
        loaded!.UpdatedAt = DateTime.UtcNow;

        await using (var deleter = new SNMDbContext(options))
        {
            deleter.AssistantConversations.Remove(await deleter.AssistantConversations.SingleAsync());
            await deleter.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repo.SaveTurnAsync(loaded));
    }

    private static async Task<(SNMDbContext Db, Guid ConversationId, Guid CustomerId)> SeedConversationAsync()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new SNMDbContext(options);
        var conversationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        db.AssistantConversations.Add(new AssistantConversation
        {
            Id = conversationId,
            CustomerId = customerId,
            Status = AssistantConversationStatus.Active,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });
        await db.SaveChangesAsync();
        return (db, conversationId, customerId);
    }
}
