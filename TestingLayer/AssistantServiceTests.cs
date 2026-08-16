using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Assistant;
using ApplicationLayer.Services.Carts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
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
        _metadata.Setup(repository => repository.GetActiveCatalogsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog());
        _metadata.Setup(repository => repository.GetCustomerProfileAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerFoodProfile?)null);
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
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
        _foods.Verify(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
        _llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_EmptyEligible_SkipsStageB_AndReturnsEmptyList()
    {
        SetupIntent(FoodIntent());
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

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
            .ReturnsAsync([]);

        await Send("Dưới 70k");

        Assert.NotNull(captured);
        Assert.Equal(70000m, captured!.BudgetMax);
    }

    [Fact]
    public async Task SendMessage_ProfileAllergy_NotOverriddenByIntent()
    {
        var allergenId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var profile = new CustomerFoodProfile
        {
            CustomerId = _conversation.CustomerId,
            AllergenExclusions = { new CustomerAllergenExclusion { AllergenId = allergenId, Allergen = new Allergen { Id = allergenId, Code = "CRUSTACEAN", Name = "Crustacean" } } }
        };
        _metadata.Setup(repository => repository.GetCustomerProfileAsync(_conversation.CustomerId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        SetupIntent("""
            { "intent": "FOOD_RECOMMENDATION", "needsLocation": false,
              "hardConstraints": { "allergenCodes": [] }, "structuredPreferences": {},
              "semanticPreferences": ["thích hải sản"], "semanticAvoidances": [] }
            """);
        AssistantFoodQueryCriteria? captured = null;
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantFoodQueryCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync([]);

        await Send("Gợi ý món ngon, mình thích hải sản");

        Assert.Contains(allergenId, captured!.AllergenExclusionIds);
    }

    [Fact]
    public async Task SendMessage_Timeout_Throws503()
    {
        _llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        var exception = await Assert.ThrowsAsync<AppException>(() => Send("Món cay"));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
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
        _llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _foods.Verify(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private static AssistantEligibleFood CartFood(Guid foodId, bool available, decimal price)
    {
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            Name = "Chợ",
            Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active
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
            Category = new FoodCategory { Id = Guid.NewGuid(), Name = "Món", Code = "MAIN" }
        };
        return new AssistantEligibleFood { FoodItem = food, EffectivePrice = price };
    }

    private static FoodSemanticCatalogSet Catalog()
        => new(
            [new Ingredient { Id = Guid.NewGuid(), Code = "PORK", Name = "Pork", IsActive = true }],
            [new Allergen { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Code = "CRUSTACEAN", Name = "Crustacean", IsActive = true }],
            [],
            [],
            [new TasteProfile { Id = Guid.NewGuid(), Code = "SPICY", Name = "Spicy", IsActive = true }]);
}
