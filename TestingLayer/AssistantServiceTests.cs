using System.Text.Json;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Assistant;
using ApplicationLayer.Services.Carts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class AssistantServiceTests
{
    private readonly Mock<ILanguageModelClient> _llm = new();
    private readonly Mock<IAssistantConversationRepository> _conversations = new();
    private readonly Mock<IAssistantFoodQueryRepository> _foods = new();
    private readonly Mock<IFoodSemanticMetadataRepository> _metadata = new();
    private readonly Mock<INightMarketRepository> _markets = new();
    private readonly Mock<ICartService> _carts = new();
    private readonly AssistantService _service;
    private readonly AssistantConversation _conversation;

    public AssistantServiceTests()
    {
        _conversation = new AssistantConversation
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Status = AssistantConversationStatus.Active,
            Messages = new List<AssistantMessage>(),
            MealPlans = new List<AssistantMealPlan>()
        };
        _conversations.Setup(repository => repository.GetOwnedAsync(_conversation.Id, _conversation.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_conversation);
        _conversations.Setup(repository => repository.GetRecentMessagesAsync(_conversation.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _conversations.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _conversations.Setup(repository => repository.SaveTurnAsync(It.IsAny<AssistantConversation>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _conversations.Setup(repository => repository.AddMessage(It.IsAny<AssistantMessage>()))
            .Callback<AssistantMessage>(message => _conversation.Messages.Add(message));
        _conversations.Setup(repository => repository.AddMealPlan(It.IsAny<AssistantMealPlan>()))
            .Callback<AssistantMealPlan>(plan => _conversation.MealPlans.Add(plan));
        _metadata.Setup(repository => repository.GetActiveCatalogsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog());
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantFoodQueryResult());
        _foods.Setup(repository => repository.CountNotDeletedFoodItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var assistantOptions = Options.Create(new AssistantOptions { CandidateBatchSize = 30, MaxRecommendations = 8, MinimumCompatibilityScore = 0.4, MaxMealPlanOptions = 3 });
        var openAi = Options.Create(new OpenAiOptions { Enabled = true, ApiKey = "test-key" });
        _service = new AssistantService(
            _conversations.Object,
            _foods.Object,
            _metadata.Object,
            _markets.Object,
            _carts.Object,
            new AssistantIntentInterpreter(_llm.Object, openAi),
            new AssistantSemanticMatcher(_llm.Object, assistantOptions, openAi),
            new AssistantCompatibilityScorer(assistantOptions),
            new AssistantMealPlanComposer(_llm.Object, new AssistantMealPlanValidator(assistantOptions), assistantOptions, openAi),
            new AssistantReplyComposer(),
            assistantOptions,
            openAi,
            TimeProvider.System,
            NullLogger<AssistantService>.Instance);
    }

    [Fact]
    public async Task SendMessage_LocationRequired_DoesNotQueryFoodsOrStageB()
    {
        SetupIntent("""
            { "intent": "FOOD_RECOMMENDATION", "needsLocation": true,
              "hardConstraints": {}, "structuredPreferences": {},
              "semanticPreferences": [], "semanticAvoidances": [] }
            """);

        var exception = await Assert.ThrowsAsync<AppException>(() => Send("Món gần tôi"));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("ASSISTANT_LOCATION_REQUIRED", exception.ErrorCode);
        Assert.Equal(AssistantConversationStatus.LocationPending, _conversation.Status);
        Assert.Equal("Món gần tôi", _conversation.PendingUserMessage);
        Assert.False(string.IsNullOrWhiteSpace(_conversation.PendingParsedIntentJson));
        _foods.Verify(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
        _llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_Chitchat_SkipsStageB()
    {
        SetupIntent("""
            { "intent": "CHITCHAT", "needsLocation": false, "assistantReply": "Chào bạn, mình sẵn sàng gợi ý món chợ đêm.",
              "hardConstraints": {}, "structuredPreferences": {},
              "semanticPreferences": [], "semanticAvoidances": [] }
            """);

        var result = await Send("Xin chào");

        Assert.True(result.Success);
        Assert.Equal(AssistantIntentKind.CHITCHAT, result.Data!.Intent);
        Assert.Empty(result.Data.Recommendations);
        var user = _conversation.Messages.Single(item => item.Role == AssistantMessageRole.User);
        var assistant = _conversation.Messages.Single(item => item.Role == AssistantMessageRole.Assistant);
        Assert.Equal("Xin chào", user.Content);
        Assert.False(string.IsNullOrWhiteSpace(assistant.Content));
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.NotEqual(Guid.Empty, assistant.Id);
        Assert.NotEqual(default, user.CreatedAt);
        Assert.NotEqual(default, assistant.CreatedAt);
        Assert.Equal(user.CreatedAt, assistant.CreatedAt);
        var ordered = _conversation.Messages
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToArray();
        Assert.Equal(2, ordered.Length);
        Assert.True(ordered[0].Id.CompareTo(ordered[1].Id) < 0);
        _conversations.Verify(repository => repository.SaveTurnAsync(_conversation, It.IsAny<CancellationToken>()), Times.Once);
        _conversations.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _foods.Verify(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_EmptyEligible_SkipsStageB_AndReturnsEmptyList()
    {
        SetupIntent(FoodIntent());
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantFoodQueryResult());

        var result = await Send("Món cay");

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Recommendations);
        Assert.Contains("chưa có món", result.Data.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.Data.Diagnostics!.EligibleCount);
        _llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_HardBudget_IsPassedInSqlCriteria()
    {
        SetupIntent("""
            { "intent": "FOOD_RECOMMENDATION", "needsLocation": false, "budgetMax": 70000,
              "hardConstraints": { "allergenCodes": [] }, "structuredPreferences": {},
              "semanticPreferences": [], "semanticAvoidances": [] }
            """);
        AssistantFoodQueryCriteria? captured = null;
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantFoodQueryCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync(new AssistantFoodQueryResult());

        await Send("Dưới 70k");

        Assert.NotNull(captured);
        Assert.Equal(70000m, captured!.BudgetMax);
    }

    [Fact]
    public async Task SendMessage_UsesStructuredPartySizeAndBudget_WithoutRewritingPrompt()
    {
        string? captured = null;
        _llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, CancellationToken>((_, user, _, _) => captured = user)
            .ReturnsAsync("""
                { "intent": "MEAL_PLAN", "needsLocation": false, "partySize": 9, "budgetMax": 10000,
                  "hardConstraints": {}, "structuredPreferences": {},
                  "semanticPreferences": [], "semanticAvoidances": [] }
                """);

        var result = await _service.SendMessageAsync(
            _conversation.CustomerId,
            _conversation.Id,
            new SendAssistantMessageRequest
            {
                Message = "Ăn tối bình dân, món dễ chia sẻ",
                PartySize = 4,
                Budget = 300_000m
            });

        Assert.True(result.Success);
        Assert.Equal(4, result.Data!.Diagnostics!.ParsedIntent!.PartySize);
        Assert.Equal(300_000m, result.Data.Diagnostics.ParsedIntent.BudgetMax);
        Assert.Equal("Ăn tối bình dân, món dễ chia sẻ", _conversation.Messages.First(item => item.Role == AssistantMessageRole.User).Content);
        Assert.NotNull(captured);
        using var payload = JsonDocument.Parse(captured);
        Assert.Equal("Ăn tối bình dân, món dễ chia sẻ", payload.RootElement.GetProperty("originalMessage").GetString());
        Assert.DoesNotContain("4 người", captured, StringComparison.Ordinal);
        Assert.DoesNotContain("300k", captured, StringComparison.OrdinalIgnoreCase);
        var explicitContext = payload.RootElement.GetProperty("explicitContext");
        Assert.Equal(4, explicitContext.GetProperty("partySize").GetInt32());
        Assert.Equal(300000m, explicitContext.GetProperty("budget").GetDecimal());
        Assert.False(payload.RootElement.TryGetProperty("customerFoodProfile", out _));
    }

    [Fact]
    public async Task SendMessage_MealPlanWithoutPartySize_Throws()
    {
        SetupIntent("""
            { "intent": "MEAL_PLAN", "needsLocation": false, "budgetMax": 200000,
              "hardConstraints": {}, "structuredPreferences": {},
              "semanticPreferences": [], "semanticAvoidances": [] }
            """);

        var exception = await Assert.ThrowsAsync<AppException>(() => Send("Lên thực đơn"));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("ASSISTANT_INVALID_REQUEST", exception.ErrorCode);
        _foods.Verify(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_Timeout_Throws503()
    {
        _llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        var exception = await Assert.ThrowsAsync<AppException>(() => Send("Món cay"));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.Timeout, AssistantProviderFailure.Reason(exception));
        Assert.Empty(_conversation.Messages);
        _conversations.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _conversations.Verify(repository => repository.SaveTurnAsync(It.IsAny<AssistantConversation>(), It.IsAny<CancellationToken>()), Times.Never);
        _foods.Verify(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_EmptyApiKey_FailsProviderUnavailable()
    {
        var assistantOptions = Options.Create(new AssistantOptions());
        var openAi = Options.Create(new OpenAiOptions { Enabled = true, ApiKey = "" });
        var service = new AssistantService(
            _conversations.Object, _foods.Object, _metadata.Object, _markets.Object, _carts.Object,
            new AssistantIntentInterpreter(_llm.Object, openAi),
            new AssistantSemanticMatcher(_llm.Object, assistantOptions, openAi),
            new AssistantCompatibilityScorer(assistantOptions),
            new AssistantMealPlanComposer(_llm.Object, new AssistantMealPlanValidator(assistantOptions), assistantOptions, openAi),
            new AssistantReplyComposer(),
            assistantOptions, openAi, TimeProvider.System, NullLogger<AssistantService>.Instance);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.SendMessageAsync(_conversation.CustomerId, _conversation.Id, new SendAssistantMessageRequest { Message = "hi" }));

        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.MissingKey, AssistantProviderFailure.Reason(exception));
        _llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _conversations.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _conversations.Verify(repository => repository.SaveTurnAsync(It.IsAny<AssistantConversation>(), It.IsAny<CancellationToken>()), Times.Never);
        _foods.Verify(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_SaveChangesConflict_IsDatabaseException_NotProviderUnavailable()
    {
        SetupIntent("""
            { "intent": "CHITCHAT", "needsLocation": false, "assistantReply": "Chào bạn.",
              "hardConstraints": {}, "structuredPreferences": {},
              "semanticPreferences": [], "semanticAvoidances": [] }
            """);
        _conversations.Setup(repository => repository.SaveTurnAsync(It.IsAny<AssistantConversation>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "could not update",
                new PostgresException("duplicate key", "ERROR", "ERROR", "23505")));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => Send("Xin chào"));

        Assert.IsNotType<AppException>(exception);
        Assert.Equal("23505", (exception.InnerException as PostgresException)?.SqlState);
        Assert.Equal(2, _conversation.Messages.Count);
    }

    [Fact]
    public async Task SendMessage_ResumeWithPendingIntent_SkipsStageA()
    {
        SetupIntent("""
            { "intent": "FOOD_RECOMMENDATION", "needsLocation": true, "budgetMax": 80000,
              "hardConstraints": { "maxSpiceLevel": "MILD" }, "structuredPreferences": {},
              "semanticPreferences": ["cay nhẹ"], "semanticAvoidances": [] }
            """);

        await Assert.ThrowsAsync<AppException>(() => Send("Món gần tôi"));
        _llm.Invocations.Clear();
        _conversation.Status = AssistantConversationStatus.LocationPending;

        var result = await _service.SendMessageAsync(
            _conversation.CustomerId,
            _conversation.Id,
            new SendAssistantMessageRequest
            {
                Message = "Món gần tôi",
                Latitude = 10.77,
                Longitude = 106.69
            });

        Assert.True(result.Success);
        Assert.Equal(AssistantIntentKind.FOOD_RECOMMENDATION, result.Data!.Intent);
        Assert.Equal(80000m, result.Data.Diagnostics!.ParsedIntent!.BudgetMax);
        Assert.Contains("cay nhẹ", result.Data.Diagnostics.ParsedIntent.SemanticPreferences);
        _llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _foods.Verify(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMealPlanToCart_RequeriesCurrentPriceThenAdds()
    {
        var foodId = Guid.NewGuid();
        var planId = SetupPlan(foodId, quantity: 2, snapshotPrice: 35_000m);
        _foods.Setup(repository => repository.GetCurrentByIdAsync(foodId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartFood(foodId, available: true, price: 35_000m));
        _carts.Setup(service => service.AddItemsAsync(_conversation.CustomerId, It.IsAny<AddCartItemsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<ApplicationLayer.DTOs.Responses.CartBatchAddResponse>.SuccessResponse(new()));

        await _service.AddMealPlanToCartAsync(_conversation.CustomerId, _conversation.Id, planId);

        _foods.Verify(repository => repository.GetCurrentByIdAsync(foodId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _carts.Verify(service => service.AddItemsAsync(
            _conversation.CustomerId,
            It.Is<AddCartItemsRequest>(request =>
                request.Items.Count == 1
                && request.Items.First().FoodItemId == foodId
                && request.Items.First().Quantity == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMealPlanToCart_FoodUnavailable_ThrowsConflict()
    {
        var foodId = Guid.NewGuid();
        var planId = SetupPlan(foodId, quantity: 1, snapshotPrice: 35_000m);
        _foods.Setup(repository => repository.GetCurrentByIdAsync(foodId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartFood(foodId, available: false, price: 35_000m));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _service.AddMealPlanToCartAsync(_conversation.CustomerId, _conversation.Id, planId));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("ASSISTANT_MEAL_PLAN_UNAVAILABLE", exception.ErrorCode);
        _carts.Verify(service => service.AddItemsAsync(It.IsAny<Guid>(), It.IsAny<AddCartItemsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddMealPlanToCart_PriceChanged_ThrowsConflict()
    {
        var foodId = Guid.NewGuid();
        var planId = SetupPlan(foodId, quantity: 1, snapshotPrice: 35_000m);
        _foods.Setup(repository => repository.GetCurrentByIdAsync(foodId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartFood(foodId, available: true, price: 49_000m));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _service.AddMealPlanToCartAsync(_conversation.CustomerId, _conversation.Id, planId));

        Assert.Equal("ASSISTANT_MEAL_PLAN_PRICE_CHANGED", exception.ErrorCode);
        _carts.Verify(service => service.AddItemsAsync(It.IsAny<Guid>(), It.IsAny<AddCartItemsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Assistant_DoesNotIntroduceSecondCartEntity()
    {
        var names = typeof(Cart).Assembly.GetTypes().Select(type => type.Name).ToArray();
        Assert.Contains(nameof(Cart), names);
        Assert.Contains(nameof(CartItem), names);
        Assert.DoesNotContain(names, name => name.Contains("AiCart", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Equals("AssistantCart", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("CustomerFoodProfile", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateMealPlanItemQuantity_RecalculatesTotals_AllowsOverBudget()
    {
        var foodId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var plan = MutationPlan(foodId, itemId, quantity: 1, snapshotPrice: 40_000m, partySize: 2, budget: 50_000m);
        _foods.Setup(repository => repository.GetCurrentByIdAsync(foodId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartFood(foodId, available: true, price: 40_000m));

        var result = await _service.UpdateMealPlanItemQuantityAsync(
            _conversation.CustomerId,
            _conversation.Id,
            plan.Id,
            itemId,
            new UpdateAssistantMealPlanItemQuantityRequest { Quantity = 3 });

        Assert.True(result.Success);
        Assert.Equal(3, plan.Items.Single().Quantity);
        Assert.Equal(120_000m, result.Data!.TotalPrice);
        Assert.Equal(70_000m, result.Data.OverBudgetAmount);
        Assert.Equal(-70_000m, result.Data.RemainingBudget);
        Assert.Contains("OVER_BUDGET", result.Data.Warnings);
        Assert.Equal(itemId, result.Data.Items.Single().PlanItemId);
        Assert.Equal(3, result.Data.Items.Single().Quantity);
    }

    [Fact]
    public async Task RemoveMealPlanItem_DoesNotAutoAddFoods_AndWarnsWhenServingsInsufficient()
    {
        var keepId = Guid.NewGuid();
        var removeId = Guid.NewGuid();
        var keepItemId = Guid.NewGuid();
        var removeItemId = Guid.NewGuid();
        var plan = new AssistantMealPlan
        {
            Id = Guid.NewGuid(),
            ConversationId = _conversation.Id,
            CustomerId = _conversation.CustomerId,
            NightMarketId = Guid.NewGuid(),
            PartySize = 4,
            BudgetMax = 400_000m,
            Items =
            {
                new AssistantMealPlanItem { Id = keepItemId, FoodItemId = keepId, Quantity = 1, DisplayOrder = 0, UnitPriceSnapshot = 30_000m },
                new AssistantMealPlanItem { Id = removeItemId, FoodItemId = removeId, Quantity = 1, DisplayOrder = 1, UnitPriceSnapshot = 30_000m }
            }
        };
        _conversations.Setup(repository => repository.GetOwnedMealPlanAsync(_conversation.Id, plan.Id, _conversation.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _foods.Setup(repository => repository.GetCurrentByIdAsync(keepId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartFood(keepId, available: true, price: 30_000m, servings: 1));

        var result = await _service.RemoveMealPlanItemAsync(_conversation.CustomerId, _conversation.Id, plan.Id, removeItemId);

        Assert.True(result.Success);
        Assert.Single(plan.Items);
        Assert.Equal(keepItemId, plan.Items.Single().Id);
        Assert.Contains("SERVINGS_INSUFFICIENT", result.Data!.Warnings);
        _foods.Verify(repository => repository.GetCurrentByIdAsync(removeId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceMealPlanItem_RejectsIdOutsideBackendPool()
    {
        var currentId = Guid.NewGuid();
        var allowedId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var plan = MutationPlan(currentId, itemId, quantity: 1, snapshotPrice: 35_000m, partySize: 2, budget: 200_000m);
        var allowed = CartFood(allowedId, available: true, price: 32_000m);
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryResult(CartFood(currentId, available: true, price: 35_000m), allowed));
        _llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($$"""{ "scores": [ { "foodItemId": "{{allowedId}}", "semanticCompatibility": 1, "reasons": ["khớp yêu cầu"] } ] }""");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _service.ReplaceMealPlanItemAsync(
                _conversation.CustomerId,
                _conversation.Id,
                plan.Id,
                itemId,
                new ReplaceAssistantMealPlanItemRequest { ReplacementFoodItemId = outsiderId }));

        Assert.Equal("PLAN_CHANGED", exception.ErrorCode);
        Assert.Equal(currentId, plan.Items.Single().FoodItemId);
    }

    [Fact]
    public async Task GetMealPlanItemReplacements_HallucinatedId_Throws503()
    {
        var currentId = Guid.NewGuid();
        var allowedId = Guid.NewGuid();
        var fakeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var plan = MutationPlan(currentId, itemId, quantity: 1, snapshotPrice: 35_000m, partySize: 2, budget: 200_000m);
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryResult(CartFood(allowedId, available: true, price: 32_000m)));
        _llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($$"""{ "scores": [ { "foodItemId": "{{fakeId}}", "semanticCompatibility": 1, "reasons": ["bịa"] } ] }""");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _service.GetMealPlanItemReplacementsAsync(_conversation.CustomerId, _conversation.Id, plan.Id, itemId));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.HallucinatedId, AssistantProviderFailure.Reason(exception));
    }

    [Fact]
    public async Task AddMealPlanToCart_AllValid_AddsEveryItemViaCartService()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var planId = Guid.NewGuid();
        _conversations.Setup(repository => repository.GetOwnedMealPlanAsync(_conversation.Id, planId, _conversation.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantMealPlan
            {
                Id = planId,
                ConversationId = _conversation.Id,
                CustomerId = _conversation.CustomerId,
                Items =
                {
                    new AssistantMealPlanItem { FoodItemId = first, Quantity = 1, DisplayOrder = 0, UnitPriceSnapshot = 20_000m },
                    new AssistantMealPlanItem { FoodItemId = second, Quantity = 2, DisplayOrder = 1, UnitPriceSnapshot = 30_000m }
                }
            });
        _foods.Setup(repository => repository.GetCurrentByIdAsync(first, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartFood(first, available: true, price: 20_000m));
        _foods.Setup(repository => repository.GetCurrentByIdAsync(second, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartFood(second, available: true, price: 30_000m));
        _carts.Setup(service => service.AddItemsAsync(_conversation.CustomerId, It.IsAny<AddCartItemsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<ApplicationLayer.DTOs.Responses.CartBatchAddResponse>.SuccessResponse(new()));

        await _service.AddMealPlanToCartAsync(_conversation.CustomerId, _conversation.Id, planId);

        _carts.Verify(service => service.AddItemsAsync(
            _conversation.CustomerId,
            It.Is<AddCartItemsRequest>(request =>
                request.Items.Count == 2
                && request.Items.Any(item => item.FoodItemId == first && item.Quantity == 1)
                && request.Items.Any(item => item.FoodItemId == second && item.Quantity == 2)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private Task<ApiResponse<ApplicationLayer.DTOs.Responses.AssistantTurnResponse>> Send(string message)
        => _service.SendMessageAsync(_conversation.CustomerId, _conversation.Id, new SendAssistantMessageRequest { Message = message });

    private void SetupIntent(string json)
        => _llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

    private static string FoodIntent()
        => """
            { "intent": "FOOD_RECOMMENDATION", "needsLocation": false,
              "hardConstraints": {}, "structuredPreferences": {},
              "semanticPreferences": [], "semanticAvoidances": [] }
            """;

    private Guid SetupPlan(Guid foodId, int quantity, decimal snapshotPrice)
    {
        var planId = Guid.NewGuid();
        _conversations.Setup(repository => repository.GetOwnedMealPlanAsync(_conversation.Id, planId, _conversation.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantMealPlan
            {
                Id = planId,
                ConversationId = _conversation.Id,
                CustomerId = _conversation.CustomerId,
                Items = { new AssistantMealPlanItem { FoodItemId = foodId, Quantity = quantity, DisplayOrder = 0, UnitPriceSnapshot = snapshotPrice } }
            });
        return planId;
    }

    private AssistantMealPlan MutationPlan(Guid foodId, Guid itemId, int quantity, decimal snapshotPrice, int partySize, decimal budget)
    {
        var plan = new AssistantMealPlan
        {
            Id = Guid.NewGuid(),
            ConversationId = _conversation.Id,
            CustomerId = _conversation.CustomerId,
            NightMarketId = Guid.NewGuid(),
            PartySize = partySize,
            BudgetMax = budget,
            Items = { new AssistantMealPlanItem { Id = itemId, FoodItemId = foodId, Quantity = quantity, DisplayOrder = 0, UnitPriceSnapshot = snapshotPrice } }
        };
        _conversations.Setup(repository => repository.GetOwnedMealPlanAsync(_conversation.Id, plan.Id, _conversation.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        return plan;
    }

    private static AssistantEligibleFood CartFood(Guid foodId, bool available, decimal price, int? servings = null)
    {
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            Name = "Chợ",
            Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active,
            OpeningHours = new TimeOnly(0, 0),
            ClosingHours = new TimeOnly(0, 0)
        };
        var booth = new Booth { Id = Guid.NewGuid(), BoothName = "Quầy", NightMarket = market, NightMarketId = market.Id, Status = BoothStatus.Active };
        var food = new FoodItem
        {
            Id = foodId,
            Name = "Bánh mì",
            Price = price,
            IsAvailable = available,
            Booth = booth,
            BoothId = booth.Id,
            Category = new FoodCategory { Id = Guid.NewGuid(), Name = "Món", Code = "MAIN" },
            EstimatedServingCount = servings
        };
        return new AssistantEligibleFood { FoodItem = food, EffectivePrice = price };
    }

    [Fact]
    public async Task SendMessage_ConversationMarketId_DoesNotScopeDiscoveryCriteria()
    {
        var conversationMarketId = Guid.NewGuid();
        _conversation.MarketId = conversationMarketId;
        _markets.Setup(repository => repository.CustomerVisibleExistsAsync(conversationMarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        SetupIntent(FoodIntent());
        AssistantFoodQueryCriteria? captured = null;
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantFoodQueryCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync(new AssistantFoodQueryResult());

        await Send("Bánh tráng ngon");

        Assert.NotNull(captured);
        Assert.Null(captured!.MarketId);
        Assert.Null(captured.MaxDistanceMeters);
    }

    [Fact]
    public async Task SendMessage_ExplicitRequestMarketId_ScopesDiscoveryCriteria()
    {
        var explicitMarketId = Guid.NewGuid();
        _markets.Setup(repository => repository.CustomerVisibleExistsAsync(explicitMarketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        SetupIntent(FoodIntent());
        AssistantFoodQueryCriteria? captured = null;
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantFoodQueryCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync(new AssistantFoodQueryResult());

        await _service.SendMessageAsync(
            _conversation.CustomerId,
            _conversation.Id,
            new SendAssistantMessageRequest
            {
                Message = "Bánh tráng ngon",
                MarketId = explicitMarketId
            });

        Assert.NotNull(captured);
        Assert.Equal(explicitMarketId, captured!.MarketId);
    }

    private static AssistantFoodQueryResult QueryResult(params AssistantEligibleFood[] foods)
        => new()
        {
            Foods = foods,
            Pipeline = new AssistantFoodQueryPipelineDiagnostics { AfterHardConstraints = foods.Length }
        };

    private static FoodSemanticCatalogSet Catalog()
        => new(
            [new Ingredient { Id = Guid.NewGuid(), Code = "PORK", Name = "Pork", IsActive = true }],
            [new Allergen { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Code = "CRUSTACEAN", Name = "Crustacean", IsActive = true }],
            [],
            [],
            [new TasteProfile { Id = Guid.NewGuid(), Code = "SPICY", Name = "Spicy", IsActive = true }]);
}
