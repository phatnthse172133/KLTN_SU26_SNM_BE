using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.AI.V2.Services;
using ApplicationLayer.Exceptions;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class FoodRecommendationV2Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Recommend_ReturnsBackendCandidateIdsAndPersistsNoCoordinates()
    {
        var foodId = Guid.NewGuid();
        AiRecommendationSession? saved = null;
        var service = CreateService(Intent(desired: ["bo nuong"], ingredients: ["ING_BEEF"]),
            [Candidate(foodId, ingredients: ["ING_BEEF"], methods: ["METHOD_GRILLED"])],
            capture: session => saved = session);

        var response = await service.RecommendAsync(Guid.NewGuid(), new()
        {
            Query = "bò nướng dưới 100000", MaximumPrice = 100_000, Latitude = 10.77m, Longitude = 106.70m
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(foodId, response.Data!.Items.Single().FoodId);
        Assert.NotEqual(Guid.Empty, response.Data.SessionId);
        Assert.Null(saved!.Latitude);
        Assert.Null(saved.Longitude);
    }

    [Fact]
    public async Task Recommend_NearMeWithoutLocation_ReturnsLocationRequiredWithoutReadingCandidates()
    {
        var read = new Mock<IFoodRecommendationReadRepository>(MockBehavior.Strict);
        AiRecommendationSession? saved = null;
        var service = CreateService(Intent(desired: ["bo"], near: true), [], read, session => saved = session);

        var response = await service.RecommendAsync(Guid.NewGuid(), new() { Query = "bò gần tôi" }, CancellationToken.None);

        Assert.Equal("LOCATION_REQUIRED", response.Data!.Status);
        Assert.Empty(response.Data.Items);
        Assert.NotNull(saved);
        read.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Recommend_ExplicitAllergenExclusionRejectsUnknownSafety()
    {
        var service = CreateService(Intent(desired: ["bo"], allergens: ["ALLERGEN_PEANUT"]), [Candidate(Guid.NewGuid())]);

        var response = await service.RecommendAsync(Guid.NewGuid(), new() { Query = "bò không đậu phộng" }, CancellationToken.None);

        Assert.Equal("NO_SUITABLE_RESULTS", response.Data!.Status);
        Assert.Empty(response.Data.Items);
        Assert.Empty(response.Data.NearMatches);
    }

    [Fact]
    public async Task Recommend_HardCommercialFiltersRunBeforeScoring()
    {
        var validId = Guid.NewGuid();
        var rows = new[]
        {
            Candidate(validId), Candidate(Guid.NewGuid(), available: false), Candidate(Guid.NewGuid(), deleted: true),
            Candidate(Guid.NewGuid(), price: 0), Candidate(Guid.NewGuid(), boothStatus: BoothStatus.Inactive),
            Candidate(Guid.NewGuid(), marketStatus: NightMarketStatus.Inactive), Candidate(Guid.NewGuid(), marketDeleted: true),
            Candidate(Guid.NewGuid(), categoryActive: false)
        };
        var response = await CreateService(Intent(), rows).RecommendAsync(Guid.NewGuid(), new() { Query = "món bò" }, CancellationToken.None);
        Assert.Equal(validId, response.Data!.Items.Single().FoodId);
    }

    [Fact]
    public async Task Recommend_HardIntentConstraintsRejectBudgetIngredientPreparationDietaryAndDistanceViolations()
    {
        async Task AssertNoResults(FoodRecommendationIntent intent, FoodRecommendationCandidate candidate, CreateFoodRecommendationV2Request? request = null)
        {
            var response = await CreateService(intent, [candidate]).RecommendAsync(Guid.NewGuid(), request ?? new() { Query = "món bò" }, CancellationToken.None);
            Assert.Equal("NO_SUITABLE_RESULTS", response.Data!.Status);
        }
        var budget = Intent(); budget.MaximumPrice = 50_000;
        await AssertNoResults(budget, Candidate(Guid.NewGuid(), price: 80_000));
        var excluded = Intent(); excluded.ExcludedIngredientCodes = ["ING_BEEF"];
        await AssertNoResults(excluded, Candidate(Guid.NewGuid(), ingredients: ["ING_BEEF"]));
        var preparation = Intent(); preparation.AvoidedPreparationMethodCodes = ["METHOD_GRILLED"];
        await AssertNoResults(preparation, Candidate(Guid.NewGuid(), methods: ["METHOD_GRILLED"]));
        var dietary = Intent(); dietary.DietaryRequirementCodes = ["DIET_VEGETARIAN"];
        await AssertNoResults(dietary, Candidate(Guid.NewGuid()));
        var distance = Intent(); distance.MaximumDistanceMeters = 1_000;
        await AssertNoResults(distance, Candidate(Guid.NewGuid(), marketLatitude: null, marketLongitude: null),
            new() { Query = "món bò gần tôi", Latitude = 10.77m, Longitude = 106.70m, MaxDistanceMeters = 1_000 });
    }

    [Fact]
    public async Task Recommend_ProviderExplanationFailureUsesDeterministicReason()
    {
        var explanation = new Mock<IAiExplanationGenerator>();
        explanation.Setup(value => value.GenerateFoodRecommendationReasonAsync(It.IsAny<FoodRecommendationExplanationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGeneratedTextResult { IsSuccess = false, UsedFallback = true, FailureCategory = AiProviderFailureCategory.INVALID_RESPONSE });
        var service = CreateService(Intent(desired: ["bo"]), [Candidate(Guid.NewGuid())], explanation: explanation.Object);

        var response = await service.RecommendAsync(Guid.NewGuid(), new() { Query = "món bò" }, CancellationToken.None);

        Assert.True(response.Data!.UsedProviderFallback);
        Assert.False(string.IsNullOrWhiteSpace(response.Data.Items.Single().Reason));
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    public async Task Recommend_InvalidQueryIsRejectedBeforeProvider(string query)
    {
        var extractor = new Mock<IAiIntentExtractor>(MockBehavior.Strict);
        var service = CreateService(Intent(), [], extractor: extractor.Object);

        var error = await Assert.ThrowsAsync<AppException>(() => service.RecommendAsync(Guid.NewGuid(), new() { Query = query }, CancellationToken.None));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("AI_INVALID_REQUEST", error.ErrorCode);
        extractor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AiProviderFailureCategory.TIMEOUT, "AI_PROVIDER_UNAVAILABLE")]
    [InlineData(AiProviderFailureCategory.INVALID_RESPONSE, "AI_PROVIDER_INVALID_RESPONSE")]
    public async Task Recommend_ProviderAndFallbackFailureMapsTypedServiceUnavailable(AiProviderFailureCategory category, string expectedCode)
    {
        var extractor = new Mock<IAiIntentExtractor>();
        extractor.Setup(value => value.ExtractFoodRecommendationIntentAsync(It.IsAny<FoodRecommendationIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoodRecommendationIntentExtractionResult { IsSuccess = false, UsedFallback = true, FailureCategory = category });
        var service = CreateService(Intent(), [], extractor: extractor.Object);
        var error = await Assert.ThrowsAsync<AppException>(() => service.RecommendAsync(Guid.NewGuid(), new() { Query = "món bò" }, CancellationToken.None));
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(expectedCode, error.ErrorCode);
    }

    [Fact]
    public void Ranker_UsesStableBackendEvidenceAndNeverExceedsOneHundred()
    {
        var options = Options.Create(new RecommendationV2Options { StrongMatchThreshold = 70, NearMatchThreshold = 50 });
        var ranker = new FoodRecommendationRanker(options);
        var intent = Intent(desired: ["bo nuong"], ingredients: ["ING_BEEF"]);
        intent.PreparationMethodCodes = ["METHOD_GRILLED"];
        intent.MaximumPrice = 100_000;

        var ranked = ranker.Rank(intent, Candidate(Guid.NewGuid(), ingredients: ["ING_BEEF"], methods: ["METHOD_GRILLED"]),
            new SemanticMatchResult(1m, ["bo nuong"]), 500);

        Assert.InRange(ranked.BaseScore, 0m, 100m);
        Assert.Contains("ING_BEEF", ranked.Evidence.Ingredients);
        Assert.Equal(ranked.BaseScore, ranked.Breakdown.FinalScore);
    }

    [Fact]
    public void Ranker_MissingLocationAndMetadataDoNotReceiveFullPoints()
    {
        var options = Options.Create(new RecommendationV2Options());
        var ranked = new FoodRecommendationRanker(options).Rank(new FoodRecommendationIntent(), Candidate(Guid.NewGuid(), rating: null, reviewCount: 0),
            new SemanticMatchResult(0, []), null);
        Assert.Equal(0, ranked.Breakdown.DistanceScore);
        Assert.True(ranked.BaseScore < 30);
        Assert.Equal(RecommendationMatchTier.LOW_MATCH, ranked.Tier);
    }

    [Fact]
    public async Task Recommend_NameOnlyQueryUsesAccentInsensitiveFoodNameMatching()
    {
        var parser = new DeterministicFoodIntentParser();
        var extractor = new Mock<IAiIntentExtractor>();
        extractor.Setup(value => value.ExtractFoodRecommendationIntentAsync(It.IsAny<FoodRecommendationIntentRequest>(), It.IsAny<CancellationToken>()))
            .Returns<FoodRecommendationIntentRequest, CancellationToken>((request, _) => Task.FromResult(parser.Parse(request)));
        var service = CreateService(Intent(), [Candidate(Guid.NewGuid(), foodName: "Phở bò")], extractor: extractor.Object);

        var response = await service.RecommendAsync(Guid.NewGuid(), new() { Query = "pho" }, CancellationToken.None);

        Assert.NotEqual("NO_SUITABLE_RESULTS", response.Data!.Status);
        Assert.Equal("Phở bò", response.Data.Items.Concat(response.Data.NearMatches).Single().FoodName);
    }

    [Fact]
    public void ContextualRefreshingQueryRanksColdSearchTextAsRelevant()
    {
        var intent = new DeterministicFoodIntentParser().Parse(new FoodRecommendationIntentRequest(
            "Something refreshing", new([], [], [], [], [], [], []), "en", "vi")).ParsedResult!;
        var candidate = Candidate(Guid.NewGuid(), foodName: "Nước sâm", servingTemperature: ServingTemperature.COLD, searchText: "thanh mát giải nhiệt");
        var semantic = new DeterministicFoodSemanticMatcher().Match(intent, candidate);
        var ranked = new FoodRecommendationRanker(Options.Create(new RecommendationV2Options())).Rank(intent, candidate, semantic, null);

        Assert.True(semantic.Score > 0);
        Assert.NotEqual(RecommendationMatchTier.LOW_MATCH, ranked.Tier);
    }

    [Fact]
    public void DiversityReranker_PrefersAnotherBoothInsideConfiguredScoreWindow()
    {
        var firstBooth = Guid.NewGuid();
        var otherBooth = Guid.NewGuid();
        var reranker = new FoodRecommendationDiversityReranker(Options.Create(new RecommendationV2Options
        { MaximumSameBoothInTopResults = 1, DiversityScoreWindow = 4 }));
        var values = new[]
        {
            Ranked(Guid.NewGuid(), firstBooth, 90), Ranked(Guid.NewGuid(), firstBooth, 89), Ranked(Guid.NewGuid(), otherBooth, 88)
        };

        var result = reranker.Rerank(values, FoodRecommendationSortPreference.BEST_MATCH).ToArray();

        Assert.Equal(otherBooth, result[1].Candidate.BoothId);
        Assert.Equal(firstBooth, result[2].Candidate.BoothId);
    }

    [Fact]
    public void DiversityReranker_KeepsSuperiorCandidateAndStableTier()
    {
        var booth = Guid.NewGuid();
        var reranker = new FoodRecommendationDiversityReranker(Options.Create(new RecommendationV2Options
        { MaximumSameBoothInTopResults = 1, DiversityScoreWindow = 4 }));
        var superior = Ranked(Guid.Parse("00000000-0000-0000-0000-000000000001"), booth, 95);
        var lower = Ranked(Guid.Parse("00000000-0000-0000-0000-000000000002"), booth, 80);
        var output = reranker.Rerank([lower, superior], FoodRecommendationSortPreference.BEST_MATCH).ToArray();
        Assert.Same(superior, output[0]);
        Assert.All(output, value => Assert.Equal(RecommendationMatchTier.STRONG_MATCH, value.Tier));
    }

    [Fact]
    public void SortPreference_UsesNearestOnlyInsideRelevanceBand()
    {
        var reranker = new FoodRecommendationDiversityReranker(Options.Create(new RecommendationV2Options { DiversityScoreWindow = 4 }));
        var farther = Ranked(Guid.NewGuid(), Guid.NewGuid(), 89, 900);
        var nearer = Ranked(Guid.NewGuid(), Guid.NewGuid(), 88, 100);
        var output = reranker.Rerank([farther, nearer], FoodRecommendationSortPreference.NEAREST_RELEVANT).ToArray();
        Assert.Same(nearer, output[0]);
    }

    [Fact]
    public async Task Feedback_RejectsFoodOutsideOwnedSessionThroughTypedRepositoryResult()
    {
        var session = new Mock<IAiRecommendationSessionRepository>();
        session.Setup(value => value.RecordFeedbackAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                AiRecommendationFeedbackAction.LIKED, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecommendationFeedbackRecordResult(RecommendationFeedbackRecordStatus.FOOD_NOT_IN_SESSION, null));
        var service = CreateService(Intent(), [], session: session.Object);

        var error = await Assert.ThrowsAsync<AppException>(() => service.FeedbackAsync(Guid.NewGuid(), Guid.NewGuid(),
            new() { FoodId = Guid.NewGuid(), Action = "LIKED" }, CancellationToken.None));

        Assert.Equal(422, error.StatusCode);
        Assert.Equal("AI_RECOMMENDATION_FOOD_NOT_IN_SESSION", error.ErrorCode);
    }

    private static FoodRecommendationV2Service CreateService(FoodRecommendationIntent intent,
        IReadOnlyCollection<FoodRecommendationCandidate> candidateRows,
        Mock<IFoodRecommendationReadRepository>? read = null,
        Action<AiRecommendationSession>? capture = null,
        IAiIntentExtractor? extractor = null,
        IAiExplanationGenerator? explanation = null,
        IAiRecommendationSessionRepository? session = null)
    {
        var extraction = new FoodRecommendationIntentExtractionResult
        {
            IsSuccess = true, ParsedResult = intent, ProviderName = "test", FailureCategory = AiProviderFailureCategory.NONE
        };
        var intentExtractor = extractor ?? Mock.Of<IAiIntentExtractor>(value =>
            value.ExtractFoodRecommendationIntentAsync(It.IsAny<FoodRecommendationIntentRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(extraction));
        var explanationGenerator = explanation ?? Mock.Of<IAiExplanationGenerator>(value =>
            value.GenerateFoodRecommendationReasonAsync(It.IsAny<FoodRecommendationExplanationContext>(), It.IsAny<CancellationToken>()) ==
            Task.FromResult(new AiGeneratedTextResult { IsSuccess = true, Text = "Phù hợp với yêu cầu dựa trên dữ liệu món hiện tại." }));
        var readRepository = read ?? new Mock<IFoodRecommendationReadRepository>();
        if (read is null)
            readRepository.Setup(value => value.GetCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(candidateRows);
        var sessionRepository = session is null ? new Mock<IAiRecommendationSessionRepository>() : null;
        sessionRepository?.Setup(value => value.SaveSessionAsync(It.IsAny<AiRecommendationSession>(), It.IsAny<IReadOnlyCollection<AiRecommendationResult>>(), It.IsAny<CancellationToken>()))
            .Callback<AiRecommendationSession, IReadOnlyCollection<AiRecommendationResult>, CancellationToken>((saved, _, _) => capture?.Invoke(saved))
            .Returns(Task.CompletedTask);
        var metadata = new Mock<IFoodSemanticMetadataRepository>();
        metadata.Setup(value => value.GetActiveCatalogsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalogs());
        metadata.Setup(value => value.GetCustomerProfileAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>())).ReturnsAsync((CustomerFoodProfile?)null);
        var options = Options.Create(new RecommendationV2Options { StrongMatchThreshold = 20, NearMatchThreshold = 10 });
        return new FoodRecommendationV2Service(intentExtractor, explanationGenerator, new DeterministicRecommendationReasonBuilder(),
            new FoodRecommendationIntentNormalizer(options), new DeterministicFoodSemanticMatcher(), new FoodRecommendationRanker(options),
            new FoodRecommendationDiversityReranker(options), readRepository.Object, session ?? sessionRepository!.Object, metadata.Object,
            options, new FixedTimeProvider(Now), NullLogger<FoodRecommendationV2Service>.Instance);
    }

    private static FoodRecommendationIntent Intent(IReadOnlyCollection<string>? desired = null, IReadOnlyCollection<string>? ingredients = null,
        IReadOnlyCollection<string>? allergens = null, bool near = false) => new()
    {
        Summary = "bo nuong", DesiredFoodTerms = desired ?? ["bo"], PreferredIngredientCodes = ingredients ?? [],
        AllergenExclusionCodes = allergens ?? [], PreferNearMe = near, Confidence = .9m
    };

    private static FoodRecommendationCandidate Candidate(Guid foodId, Guid? boothId = null,
        IReadOnlyCollection<string>? ingredients = null, IReadOnlyCollection<string>? methods = null,
        decimal price = 80_000, bool available = true, bool deleted = false, BoothStatus boothStatus = BoothStatus.Active,
        NightMarketStatus marketStatus = NightMarketStatus.Active, bool marketDeleted = false, bool categoryActive = true,
        decimal? marketLatitude = null, decimal? marketLongitude = null, decimal? rating = 4.5m, int reviewCount = 20,
        string foodName = "Bò nướng", ServingTemperature? servingTemperature = null, string? searchText = null) => new()
    {
        FoodId = foodId, FoodName = foodName, CategoryId = Guid.NewGuid(), CategoryCode = "CAT_GRILL", CategoryName = "Món nướng",
        CurrentPrice = price, IsAvailable = available, IsDeleted = deleted, CategoryIsActive = categoryActive, CategoryIsSelectable = true,
        BoothId = boothId ?? Guid.NewGuid(), BoothName = "Booth A", BoothStatus = boothStatus,
        MarketId = Guid.NewGuid(), MarketName = "Market A", MarketStatus = marketStatus, MarketModerationStatus = ModerationStatus.Active,
        MarketDeleted = marketDeleted, MarketOpenTime = new TimeOnly(0, 0), MarketCloseTime = new TimeOnly(23, 59),
        MarketLatitude = marketLatitude, MarketLongitude = marketLongitude, Rating = rating, ReviewCount = reviewCount,
        ServingTemperature = servingTemperature, SearchText = searchText,
        IngredientCodes = ingredients ?? [], PreparationMethodCodes = methods ?? [], Courses = [FoodCourse.MAIN_COURSE], SpiceLevel = FoodSpiceLevel.MILD
    };

    private static FoodSemanticCatalogSet Catalogs() => new(
        [new Ingredient { Id = Guid.NewGuid(), Code = "ING_BEEF", Name = "Bò", NormalizedName = "bo", IsActive = true }],
        [new Allergen { Id = Guid.NewGuid(), Code = "ALLERGEN_PEANUT", Name = "Đậu phộng", IsActive = true }],
        [new DietaryAttribute { Id = Guid.NewGuid(), Code = "DIET_VEGETARIAN", Name = "Chay", IsActive = true }],
        [new PreparationMethod { Id = Guid.NewGuid(), Code = "METHOD_GRILLED", Name = "Nướng", IsActive = true }], []);

    private static RankedRecommendationCandidate Ranked(Guid foodId, Guid boothId, decimal score, int? distance = null) => new()
    {
        Candidate = Candidate(foodId, boothId), Tier = RecommendationMatchTier.STRONG_MATCH, DistanceMeters = distance,
        Breakdown = new RecommendationScoreBreakdown { FinalScore = score }
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
