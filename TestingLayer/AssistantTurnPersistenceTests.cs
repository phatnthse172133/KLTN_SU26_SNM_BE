using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Assistant;
using ApplicationLayer.Services.Carts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace TestingLayer;

public sealed class AssistantTurnPersistenceTests
{
    [Fact]
    public async Task A_ConversationUpdateFailure_DoesNotCommitMessages()
    {
        var interceptor = new FailModifiedAssistantConversationInterceptor { RemainingFailures = 2 };
        var (db, repo, conversation) = await SeedTrackedAsync(interceptor);
        await using (db)
        {
            AddTurnMessages(repo, conversation.Id);
            conversation.UpdatedAt = conversation.UpdatedAt.AddSeconds(1);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repo.SaveTurnAsync(conversation));

            Assert.Equal(0, await db.AssistantMessages.CountAsync());
        }
    }

    [Fact]
    public async Task B_ConversationUpdateFailure_DoesNotCommitMealPlan()
    {
        var interceptor = new FailModifiedAssistantConversationInterceptor { RemainingFailures = 2 };
        var (db, repo, conversation) = await SeedTrackedAsync(interceptor);
        await using (db)
        {
            repo.AddMealPlan(new AssistantMealPlan
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                CustomerId = conversation.CustomerId,
                NightMarketId = Guid.NewGuid(),
                EstimatedTotal = 40_000m,
                PartySize = 2,
                CreatedAt = conversation.CreatedAt,
                Items =
                {
                    new AssistantMealPlanItem
                    {
                        Id = Guid.NewGuid(),
                        FoodItemId = Guid.NewGuid(),
                        Quantity = 1,
                        UnitPriceSnapshot = 40_000m,
                        DisplayOrder = 0
                    }
                }
            });
            conversation.UpdatedAt = conversation.UpdatedAt.AddSeconds(1);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repo.SaveTurnAsync(conversation));

            Assert.Equal(0, await db.AssistantMealPlans.CountAsync());
            Assert.Equal(0, await db.AssistantMealPlanItems.CountAsync());
        }
    }

    [Fact]
    public async Task C_RetryAfterConcurrency_DoesNotDuplicateUserMessage()
    {
        var interceptor = new FailModifiedAssistantConversationInterceptor { RemainingFailures = 1 };
        var (db, repo, conversation) = await SeedTrackedAsync(interceptor);
        await using (db)
        {
            AddTurnMessages(repo, conversation.Id);
            conversation.UpdatedAt = conversation.UpdatedAt.AddSeconds(1);

            await repo.SaveTurnAsync(conversation);

            Assert.Equal(1, await db.AssistantMessages.CountAsync(item => item.Role == AssistantMessageRole.User));
        }
    }

    [Fact]
    public async Task D_RetryAfterConcurrency_DoesNotDuplicateAssistantMessage()
    {
        var interceptor = new FailModifiedAssistantConversationInterceptor { RemainingFailures = 1 };
        var (db, repo, conversation) = await SeedTrackedAsync(interceptor);
        await using (db)
        {
            AddTurnMessages(repo, conversation.Id);
            conversation.UpdatedAt = conversation.UpdatedAt.AddSeconds(1);

            await repo.SaveTurnAsync(conversation);

            Assert.Equal(1, await db.AssistantMessages.CountAsync(item => item.Role == AssistantMessageRole.Assistant));
        }
    }

    [Fact]
    public async Task E_Success_CommitsMessagesAndHeader()
    {
        var (db, conversationId, customerId) = await SeedConversationAsync();
        await using (db)
        {
            db.ChangeTracker.Clear();
            var repo = new AssistantConversationRepository(db);
            var loaded = await repo.GetOwnedAsync(conversationId, customerId);
            Assert.NotNull(loaded);
            var now = loaded!.UpdatedAt.AddMinutes(5);
            AddTurnMessages(repo, loaded.Id, now);
            loaded.UpdatedAt = now;

            await repo.SaveTurnAsync(loaded);

            Assert.Equal(2, await db.AssistantMessages.CountAsync(item => item.ConversationId == conversationId));
            var header = await db.AssistantConversations.AsNoTracking().SingleAsync(item => item.Id == conversationId);
            Assert.Equal(now, header.UpdatedAt);
            Assert.Equal(EntityState.Unchanged, db.Entry(loaded).State);
        }
    }

    [Fact]
    public async Task F_OpenAiInvokedOnce_WhenOnlyDatabaseRetries()
    {
        var interceptor = new FailModifiedAssistantConversationInterceptor { RemainingFailures = 1 };
        var name = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(name)
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new SNMDbContext(options);
        var conversationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 8, 18, 14, 0, 0, DateTimeKind.Utc);
        db.AssistantConversations.Add(new AssistantConversation
        {
            Id = conversationId,
            CustomerId = customerId,
            Status = AssistantConversationStatus.Active,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                { "intent": "CHITCHAT", "needsLocation": false, "assistantReply": "Chào bạn.",
                  "hardConstraints": {}, "structuredPreferences": {},
                  "semanticPreferences": [], "semanticAvoidances": [] }
                """);
        var foods = new Mock<IAssistantFoodQueryRepository>();
        foods.Setup(repository => repository.CountNotDeletedFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantFoodQueryResult());
        var metadata = new Mock<IFoodSemanticMetadataRepository>();
        metadata.Setup(repository => repository.GetActiveCatalogsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoodSemanticCatalogSet([], [], [], [], []));
        var carts = new Mock<ICartService>();
        var assistantOptions = Options.Create(new AssistantOptions { CandidateBatchSize = 30, SemanticBatchMaxConcurrency = 3 });
        var openAi = Options.Create(new OpenAiOptions { Enabled = true, ApiKey = "test-key", TimeoutSeconds = 60 });
        var repo = new AssistantConversationRepository(db);
        var service = new AssistantService(
            repo,
            foods.Object,
            metadata.Object,
            carts.Object,
            new AssistantIntentInterpreter(llm.Object, openAi),
            new AssistantSemanticMatcher(llm.Object, assistantOptions, openAi),
            new AssistantCompatibilityScorer(assistantOptions),
            new AssistantMealPlanComposer(llm.Object, new AssistantMealPlanValidator(assistantOptions), assistantOptions, openAi),
            new AssistantReplyComposer(),
            assistantOptions,
            openAi,
            TimeProvider.System,
            NullLogger<AssistantService>.Instance);

        var result = await service.SendMessageAsync(
            customerId,
            conversationId,
            new SendAssistantMessageRequest { Message = "Xin chào" });

        Assert.True(result.Success);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, interceptor.SaveAttempts);
        Assert.Equal(0, interceptor.RemainingFailures);
        Assert.Equal(2, await db.AssistantMessages.CountAsync(item => item.ConversationId == conversationId));
        Assert.Equal(1, await db.AssistantMessages.CountAsync(item => item.Role == AssistantMessageRole.User));
        Assert.Equal(1, await db.AssistantMessages.CountAsync(item => item.Role == AssistantMessageRole.Assistant));
    }

    private static async Task<(SNMDbContext Db, AssistantConversationRepository Repo, AssistantConversation Conversation)> SeedTrackedAsync(
        FailModifiedAssistantConversationInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptor)
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
        db.ChangeTracker.Clear();
        var repo = new AssistantConversationRepository(db);
        var loaded = await repo.GetOwnedAsync(conversationId, customerId);
        Assert.NotNull(loaded);
        return (db, repo, loaded!);
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

    private static void AddTurnMessages(AssistantConversationRepository repo, Guid conversationId, DateTime? at = null)
    {
        var createdAt = at ?? new DateTime(2026, 8, 18, 13, 0, 0, DateTimeKind.Utc);
        repo.AddMessage(new AssistantMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = AssistantMessageRole.User,
            Content = "hi",
            CreatedAt = createdAt
        });
        repo.AddMessage(new AssistantMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = AssistantMessageRole.Assistant,
            Content = "hello",
            CreatedAt = createdAt
        });
    }

    private sealed class FailModifiedAssistantConversationInterceptor : SaveChangesInterceptor
    {
        public int RemainingFailures { get; set; }
        public int SaveAttempts { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => new(Fail(eventData, result));

        private InterceptionResult<int> Fail(DbContextEventData eventData, InterceptionResult<int> result)
        {
            var context = eventData.Context ?? throw new InvalidOperationException("DbContext was missing.");
            var hasModifiedHeader = context.ChangeTracker.Entries<AssistantConversation>()
                .Any(entry => entry.State == EntityState.Modified);
            if (!hasModifiedHeader)
                return result;
            SaveAttempts++;
            if (RemainingFailures > 0)
            {
                RemainingFailures--;
                throw new DbUpdateConcurrencyException(
                    "The database operation was expected to affect 1 row(s), but actually affected 0 row(s).");
            }

            return result;
        }
    }
}
