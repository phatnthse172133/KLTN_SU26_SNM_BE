using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.MealPlans;
using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.AI.V2.Services;
using ApplicationLayer.Exceptions;
using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class MealPlanV2ServiceTests
{
    [Fact]
    public async Task Create_returns_only_feasible_same_market_plans_and_is_idempotent()
    {
        var fixture = Fixture(3);
        var request = Request();
        var first = await fixture.Service.CreateAsync(Customer, request, default);
        Assert.Equal("SUCCESS", first.Data!.Status); Assert.Equal(3, first.Data.Plans.Count);
        Assert.Equal(3, first.Data.Plans.Select(value => value.PlanId).Distinct().Count());
        Assert.Equal(3, fixture.Repository.Sessions.Single().Plans
            .Select(plan => string.Join(',', plan.Items.Where(item => !item.IsRemoved).Select(item => item.FoodItemId).Order()))
            .Distinct().Count());
        Assert.All(first.Data.Plans, value => { Assert.True(value.IsComplete); Assert.Equal(1, value.Version); Assert.True(value.TotalPrice <= value.Budget); });
        var replay = await fixture.Service.CreateAsync(Customer, request, default);
        Assert.Equal(first.Data.SessionId, replay.Data!.SessionId); Assert.Equal(1, fixture.Extractor.Calls);
        var changed = Request(); changed.Budget++;
        var conflict = await Assert.ThrowsAsync<AppException>(() => fixture.Service.CreateAsync(Customer, changed, default));
        Assert.Equal("AI_IDEMPOTENCY_CONFLICT", conflict.ErrorCode);
    }

    [Fact]
    public async Task One_feasible_market_can_return_three_distinct_real_plans()
    {
        var fixture = Fixture(1);
        var response = await fixture.Service.CreateAsync(Customer, Request(), default);
        Assert.Equal("SUCCESS", response.Data!.Status); Assert.Equal(3, response.Data.Plans.Count);
        Assert.All(response.Data.Plans, plan => Assert.Equal(response.Data.Plans.First().Market.Id, plan.Market.Id));
        Assert.Equal(new[] { "BEST_MATCH", "BUDGET_FRIENDLY", "DIVERSE" }, response.Data.Plans.Select(plan => plan.Strategy));
    }

    [Fact]
    public async Task Empty_natural_language_request_uses_neutral_intent_without_calling_provider()
    {
        var fixture = Fixture(1);
        var request = Request(); request.Request = null; request.NaturalLanguageRequest = "   ";
        var response = await fixture.Service.CreateAsync(Customer, request, default);
        Assert.NotEmpty(response.Data!.Plans);
        Assert.Equal("NEUTRAL", response.Data.Provider);
        Assert.Equal(0, fixture.Extractor.Calls);
    }

    [Fact]
    public async Task Soft_distance_ranking_does_not_persist_ephemeral_customer_coordinates()
    {
        var fixture = Fixture(3);
        var request = Request(1); request.MaxDistanceMeters = null; request.MaximumDistanceMeters = null;

        var response = await fixture.Service.CreateAsync(Customer, request, default);

        Assert.NotNull(Assert.Single(response.Data!.Plans).Market.DistanceMeters);
        var session = Assert.Single(fixture.Repository.Sessions);
        Assert.Null(session.Latitude); Assert.Null(session.Longitude);
    }

    [Fact]
    public async Task Alternatives_replace_remove_and_regenerate_are_server_recalculated_and_versioned()
    {
        var fixture = Fixture(1);
        var created = (await fixture.Service.CreateAsync(Customer, Request(1), default)).Data!;
        var planId = Assert.Single(created.Plans).PlanId;
        var detail = (await fixture.Service.GetDetailAsync(Customer, planId, default)).Data!;
        var main = detail.CourseGroups.Single(group => group.Course == "MAIN_COURSE").Items.Single();
        var alternatives = (await fixture.Service.GetAlternativesAsync(Customer, planId, main.PlanItemId, 1, 10, default)).Data!;
        Assert.True(alternatives.Total >= 2); Assert.Equal(detail.Version, alternatives.CurrentPlanVersion);
        var replacement = alternatives.Items.First();
        var replaced = (await fixture.Service.ReplaceAsync(Customer, planId, main.PlanItemId,
            new() { ReplacementFoodId = replacement.FoodId, ExpectedPlanVersion = detail.Version }, default)).Data!;
        Assert.Equal(detail.Version + 1, replaced.Version); Assert.True(replaced.IsComplete);
        var stale = await Assert.ThrowsAsync<AppException>(() => fixture.Service.RemoveAsync(Customer, planId,
            replaced.CourseGroups.SelectMany(group => group.Items).First().PlanItemId, detail.Version, default));
        Assert.Equal("AI_PLAN_VERSION_CONFLICT", stale.ErrorCode);
        var currentMain = replaced.CourseGroups.Single(group => group.Course == "MAIN_COURSE").Items.Single();
        var regenerated = (await fixture.Service.RegenerateCourseAsync(Customer, planId, FoodCourse.MAIN_COURSE,
            new() { ExpectedPlanVersion = replaced.Version }, default)).Data!;
        Assert.Equal(replaced.Version + 1, regenerated.Version);
        Assert.DoesNotContain(regenerated.CourseGroups.SelectMany(group => group.Items), item => item.FoodId == currentMain.FoodId);
        var drink = regenerated.CourseGroups.Single(group => group.Course == "DRINK").Items.Single();
        var removed = (await fixture.Service.RemoveAsync(Customer, planId, drink.PlanItemId, regenerated.Version, default)).Data!;
        Assert.Equal(regenerated.Version + 1, removed.Version); Assert.True(removed.TotalPrice < regenerated.TotalPrice);
        Assert.True(removed.IsComplete); // Drink is optional; the serving-complete main course remains valid.
    }

    [Fact]
    public async Task Expired_session_blocks_alternatives_and_mutations_but_detail_remains_read_only()
    {
        var fixture = Fixture(1);
        var created = (await fixture.Service.CreateAsync(Customer, Request(1), default)).Data!;
        var session = fixture.Repository.Sessions.Single(); session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        var planId = Assert.Single(created.Plans).PlanId;
        var detail = (await fixture.Service.GetDetailAsync(Customer, planId, default)).Data!;
        var item = detail.CourseGroups.SelectMany(group => group.Items).First();
        var expired = await Assert.ThrowsAsync<AppException>(() => fixture.Service.GetAlternativesAsync(Customer, planId, item.PlanItemId, 1, 10, default));
        Assert.Equal("AI_PLAN_EXPIRED", expired.ErrorCode);
    }

    [Fact]
    public async Task Add_to_cart_uses_one_authoritative_batch_call_and_replays_without_duplicate_quantity()
    {
        var fixture = Fixture(1);
        var created = (await fixture.Service.CreateAsync(Customer, Request(1), default)).Data!;
        var summary = Assert.Single(created.Plans);
        var expectedFoods = (await fixture.Service.GetDetailAsync(Customer, summary.PlanId, default)).Data!
            .CourseGroups.SelectMany(group => group.Items).Select(item => item.FoodId!.Value).Order().ToArray();
        IReadOnlyCollection<(Guid FoodItemId, int Quantity)>? captured = null;
        fixture.Cart.Setup(value => value.AddItemsAsync(Customer, It.IsAny<IReadOnlyCollection<(Guid, int)>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<(Guid FoodItemId, int Quantity)>, CancellationToken>((_, items, _) => captured = items)
            .ReturnsAsync(new CartBatchAddResponse { Cart = new CartResponse { CartId = Guid.NewGuid() }, AddedFoodItemIds = expectedFoods });
        var request = new AddMealPlanToCartRequest { ExpectedPlanVersion = summary.Version, IdempotencyKey = "cart-key" };

        var first = await fixture.Service.AddToCartAsync(Customer, summary.PlanId, request, default);
        var replay = await fixture.Service.AddToCartAsync(Customer, summary.PlanId, request, default);

        Assert.Equal(expectedFoods, captured!.Select(item => item.FoodItemId).Order());
        Assert.Equal("CREATED", first.Data!.IdempotencyResult);
        Assert.Equal("REPLAYED", replay.Data!.IdempotencyResult);
        fixture.Cart.Verify(value => value.AddItemsAsync(Customer, It.IsAny<IReadOnlyCollection<(Guid, int)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static FixtureState Fixture(int markets)
    {
        var values = Enumerable.Range(1, markets).SelectMany(Candidates).ToArray();
        var extractor = new FakeExtractor(); var repository = new FakeRepository();
        var cart = new Mock<IMealPlanCartIntegrationService>();
        var metadata = new Mock<IFoodSemanticMetadataRepository>();
        metadata.Setup(value => value.GetActiveCatalogsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoodSemanticCatalogSet([], [], [], [], []));
        var service = new MealPlanV2Service(extractor, new FakeCandidates(values), repository, metadata.Object,
            new MealPlanPolicyResolver(), new MealPlanRecalculationService(), cart.Object, Options.Create(new MealPlanV2Options()),
            TimeProvider.System, NullLogger<MealPlanV2Service>.Instance);
        return new(service, repository, extractor, cart);
    }

    private static CreateMealPlanV2Request Request(int requestedPlanCount = 3) => new()
    {
        PartySize = 2, Budget = 500_000, DiningStyle = "FULL_MEAL", Request = "bua an day du",
        Latitude = 10.77m, Longitude = 106.70m, MaxDistanceMeters = 50_000,
        RequestedPlanCount = requestedPlanCount, IdempotencyKey = "create-key"
    };

    private static IEnumerable<FoodRecommendationCandidate> Candidates(int number)
    {
        var market = GuidFrom(number, 1); var booth = GuidFrom(number, 2);
        yield return Candidate(GuidFrom(number, 10), market, booth, FoodCourse.MAIN_COURSE, 100_000, 2, number);
        yield return Candidate(GuidFrom(number, 11), market, booth, FoodCourse.MAIN_COURSE, 110_000, 2, number);
        yield return Candidate(GuidFrom(number, 12), market, booth, FoodCourse.MAIN_COURSE, 120_000, 2, number);
        yield return Candidate(GuidFrom(number, 13), market, booth, FoodCourse.DRINK, 20_000, 1, number);
    }

    private static FoodRecommendationCandidate Candidate(Guid food, Guid market, Guid booth, FoodCourse course,
        decimal price, int serving, int number) => new()
    {
        FoodId = food, FoodName = $"Food {food:N}", CategoryId = GuidFrom(number, 20), CategoryCode = "MAIN", CategoryName = "Main",
        CurrentPrice = price, IsAvailable = true, CategoryIsActive = true, CategoryIsSelectable = true,
        BoothId = booth, BoothName = $"Booth {number}", BoothStatus = BoothStatus.Active,
        MarketId = market, MarketName = $"Market {number}", MarketStatus = NightMarketStatus.Active,
        MarketModerationStatus = ModerationStatus.Active, MarketOpenTime = new TimeOnly(0, 0), MarketCloseTime = new TimeOnly(23, 59, 59),
        MarketLatitude = 10.77m + number / 1000m,
        MarketLongitude = 106.70m, EstimatedServingCount = serving, Courses = [course], Rating = 4.5m, ReviewCount = 10
    };

    private static Guid GuidFrom(int a, int b) => Guid.Parse($"{a:X8}-0000-0000-0000-{b:X12}");
    private static readonly Guid Customer = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private sealed record FixtureState(MealPlanV2Service Service, FakeRepository Repository, FakeExtractor Extractor,
        Mock<IMealPlanCartIntegrationService> Cart);

    private sealed class FakeExtractor : IAiIntentExtractor
    {
        public int Calls { get; private set; }
        public Task<MealPlanIntentExtractionResult> ExtractMealPlanIntentAsync(MealPlanIntentRequest request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new MealPlanIntentExtractionResult { IsSuccess = true, ParsedResult = new MealPlanIntent { Summary = request.Query, Confidence = .5m } }); }
        public Task<FoodRecommendationIntentExtractionResult> ExtractFoodRecommendationIntentAsync(FoodRecommendationIntentRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeCandidates(IReadOnlyCollection<FoodRecommendationCandidate> values) : IMealPlanCandidateRepository
    {
        public Task<IReadOnlyCollection<FoodRecommendationCandidate>> GetCandidatesAsync(DateTime utcNow, int limit, int maximumPerMarket, CancellationToken cancellationToken)
            => Task.FromResult(values);
    }

    private sealed class FakeRepository : IMealPlanV2Repository
    {
        public List<AiMealPlanSession> Sessions { get; } = [];
        public List<AiMealPlanCartOperation> CartOperations { get; } = [];
        public Task<AiMealPlanSession?> FindSessionAsync(Guid customerId, string key, CancellationToken cancellationToken)
            => Task.FromResult(Sessions.SingleOrDefault(value => value.CustomerId == customerId && value.IdempotencyKey == key));
        public Task<AiMealPlanSession?> GetActiveSessionAsync(Guid customerId, Guid sessionId, DateTime utcNow, CancellationToken cancellationToken)
            => Task.FromResult(Sessions.SingleOrDefault(value => value.Id == sessionId && value.CustomerId == customerId && value.ExpiresAt > utcNow));
        public Task<MealPlanIdempotencyResult> SaveCreateAsync(AiMealPlanSession session, CancellationToken cancellationToken)
        {
            var existing = Sessions.SingleOrDefault(value => value.CustomerId == session.CustomerId && value.IdempotencyKey == session.IdempotencyKey);
            if (existing is not null) return Task.FromResult(new MealPlanIdempotencyResult(existing.RequestHash == session.RequestHash ? MealPlanIdempotencyStatus.EXISTING : MealPlanIdempotencyStatus.CONFLICT, existing));
            foreach (var plan in session.Plans)
            {
                plan.Session = session; plan.Market = new NightMarket { Id = plan.MarketId, Name = "Market", Address = "Address",
                    Status = NightMarketStatus.Open, ModerationStatus = ModerationStatus.Active,
                    OpeningHours = new TimeOnly(0, 0), ClosingHours = new TimeOnly(23, 59, 59) };
                foreach (var item in plan.Items)
                {
                    item.Plan = plan; item.Booth = new Booth { Id = item.BoothId!.Value, NightMarketId = plan.MarketId,
                        BoothName = item.BoothNameSnapshot, Status = BoothStatus.Active, NightMarket = plan.Market };
                    item.FoodItem = new FoodItem { Id = item.FoodItemId!.Value, BoothId = item.Booth.Id, Name = item.FoodNameSnapshot,
                        Price = item.UnitPriceSnapshot, IsAvailable = true, Booth = item.Booth,
                        Category = new FoodCategory { Id = Guid.NewGuid(), Name = "Main", IsDeleted = false } };
                }
            }
            Sessions.Add(session); return Task.FromResult(new MealPlanIdempotencyResult(MealPlanIdempotencyStatus.CREATED, session));
        }
        public Task<AiMealPlan?> GetOwnedPlanAsync(Guid customerId, Guid planId, CancellationToken cancellationToken)
            => Task.FromResult(Sessions.Where(value => value.CustomerId == customerId).SelectMany(value => value.Plans).SingleOrDefault(value => value.Id == planId));
        public async Task<IMealPlanMutation?> BeginOwnedMutationAsync(Guid customerId, Guid planId, CancellationToken cancellationToken)
            => new Mutation(await GetOwnedPlanAsync(customerId, planId, cancellationToken), CartOperations);
        public Task<int> DeleteExpiredBatchAsync(DateTime retentionCutoffUtc, int batchSize, CancellationToken cancellationToken) => Task.FromResult(0);
        private sealed class Mutation(AiMealPlan? plan, List<AiMealPlanCartOperation> operations) : IMealPlanMutation
        {
            public AiMealPlan Plan { get; } = plan!;
            public Task<AiMealPlanCartOperation?> FindCartOperationAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken)
                => Task.FromResult(operations.SingleOrDefault(value => value.CustomerId == customerId && value.IdempotencyKey == idempotencyKey));
            public void AddCartOperation(AiMealPlanCartOperation operation) => operations.Add(operation);
            public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
