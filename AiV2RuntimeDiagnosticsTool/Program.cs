using System.Text.Json;
using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.AI.V2.MealPlans;
using ApplicationLayer.AI.V2.Models;
using ApplicationLayer.AI.V2.Recommendations;
using ApplicationLayer.AI.V2.Services;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Npgsql;

var connectionEnvironmentVariable = Value("--connection-env") ?? "SNM_AI_V2_DIAGNOSTICS_CONNECTION";
var allowedDatabase = Value("--allow-database") ?? throw new ArgumentException("--allow-database is required.");
var rawConnection = Environment.GetEnvironmentVariable(connectionEnvironmentVariable);
if (string.IsNullOrWhiteSpace(rawConnection))
    throw new InvalidOperationException($"Connection environment variable '{connectionEnvironmentVariable}' is missing.");
var connection = new NpgsqlConnectionStringBuilder(rawConnection);
if (!string.Equals(connection.Database, allowedDatabase, StringComparison.Ordinal))
    throw new InvalidOperationException("Database name does not match --allow-database. Diagnostics were not started.");

var now = DateTime.UtcNow;
var options = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(connection.ConnectionString).Options;
await using var db = new SNMDbContext(options);
var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
var foods = await db.FoodItems.AsNoTracking()
    .Include(value => value.Category).Include(value => value.Booth).ThenInclude(value => value.NightMarket)
    .Include(value => value.FoodPrices).Include(value => value.AiProfile)
    .Include(value => value.Ingredients).Include(value => value.PreparationMethods)
    .Include(value => value.TasteProfiles).Include(value => value.Courses).Include(value => value.DiningPurposes)
    .AsSplitQuery().ToListAsync();
var candidates = await new FoodRecommendationReadRepository(db).GetCandidatesAsync(now, 500, CancellationToken.None);
var local = TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(now));
var phoRows = foods.Where(value => DeterministicFoodIntentParser.NormalizeText(value.Name).Contains("pho", StringComparison.Ordinal)).ToArray();
var intent = new DeterministicFoodIntentParser().Parse(
    new FoodRecommendationIntentRequest("Phở", new([], [], [], [], [], [], []))).ParsedResult!;
var scorerOptions = Options.Create(new RecommendationV2Options());
var matcher = new DeterministicFoodSemanticMatcher();
var ranker = new FoodRecommendationRanker(scorerOptions);
var profileStatusRows = await db.FoodAiProfiles.GroupBy(value => value.Status)
    .Select(group => new { Status = group.Key, Count = group.Count() }).ToArrayAsync();
if (args.Contains("--recommendations", StringComparer.Ordinal))
{
    var customerId = await db.Users.AsNoTracking().Where(value => !db.CustomerFoodProfiles.Any(profile => profile.CustomerId == value.Id))
        .OrderBy(value => value.CreatedAt).Select(value => value.Id).FirstOrDefaultAsync();
    if (customerId == Guid.Empty) customerId = await db.Users.AsNoTracking().OrderBy(value => value.CreatedAt).Select(value => value.Id).FirstAsync();
    var queries = new[]
    {
        "Không biết ăn gì", "Muốn món ngon dễ ăn", "Phở nhưng không bò", "Hải sản nướng dưới 150 nghìn",
        "Món chay, không chiên", "Cần món nóng và ít dầu", "Ưu tiên món lạnh thanh nhẹ", "Món no cho một người dưới 80 nghìn",
        "Đồ ăn chia sẻ cho nhóm bạn", "Món phổ biến giá vừa phải", "Tránh hải sản, ưu tiên gà", "Món cay nhẹ dễ mang đi",
        "Bữa tối nhẹ với rau", "Món hấp không ngọt", "Đồ uống lạnh ít ngọt", "Món chính cho gia đình",
        "Muốn ăn đậm vị nhưng không chiên", "Bữa ăn nhanh trước giờ học", "Thử đặc sản địa phương", "Món nóng cho ngày mưa"
    };
    var results = new List<object>();
    foreach (var query in queries)
    {
        await using var queryDb = new SNMDbContext(options);
        var response = (await RuntimeRecommendationService(queryDb).RecommendAsync(customerId,
            new CreateFoodRecommendationV2Request { Query = query, PageSize = 5 }, default)).Data!;
        results.Add(new
        {
            RawQuery = query, response.Status, response.UsedProviderFallback, response.ProviderRuntime,
            response.UnderstoodRequest, response.Warnings, response.Diagnostics,
            TopResults = response.Items.Concat(response.NearMatches).Take(5).Select(item => new
                { item.FoodId, item.FoodName, item.CompatibilityScore, item.MatchTier, item.MarketDistanceMeters, item.Reason }).ToArray()
        });
    }
    Console.WriteLine(JsonSerializer.Serialize(new { Database = connection.Database, CandidateRows = candidates.Count,
        RecommendationRuntime = results }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
var mealPlanRuntime = Array.Empty<object>();
if (args.Contains("--meal-plans", StringComparer.Ordinal))
{
    var customerId = await db.Users.AsNoTracking().OrderBy(value => value.CreatedAt).Select(value => value.Id).FirstAsync();
    var capture = new CaptureLogger<MealPlanV2Service>();
    var scenarios = new[]
    {
        new { Code = "A", Party = 2, Budget = 300_000m, Query = (string?)null, Style = "FULL_MEAL" },
        new { Code = "B", Party = 2, Budget = 300_000m, Query = "Muốn ăn chay", Style = "FULL_MEAL" },
        new { Code = "C", Party = 2, Budget = 300_000m, Query = "Muốn hải sản nướng", Style = "FULL_MEAL" },
        new { Code = "D", Party = 2, Budget = 300_000m, Query = "Ăn nhẹ, lạnh, không quá ngọt", Style = "LIGHT_MEAL" },
        new { Code = "E", Party = 2, Budget = 300_000m, Query = "Ăn no, không hải sản", Style = "FULL_MEAL" },
        new { Code = "F", Party = 4, Budget = 500_000m, Query = "Món dùng chung với bạn", Style = "FRIEND_GROUP" },
        new { Code = "G", Party = 1, Budget = 90_000m, Query = "Cần món gọn, no và dễ mang đi", Style = "BUDGET_FRIENDLY" },
        new { Code = "H", Party = 2, Budget = 400_000m, Query = "Bữa tối hẹn hò, ưu tiên món nóng", Style = "DATE" },
        new { Code = "I", Party = 5, Budget = 650_000m, Query = "Gia đình có trẻ nhỏ, không cay và dễ chia", Style = "FAMILY" },
        new { Code = "J", Party = 3, Budget = 180_000m, Query = "Ăn tiết kiệm, tránh đồ chiên", Style = "BUDGET_FRIENDLY" },
        new { Code = "K", Party = 2, Budget = 350_000m, Query = "Muốn thử đặc sản địa phương, vị đậm", Style = "LOCAL_SPECIALTY" },
        new { Code = "L", Party = 3, Budget = 450_000m, Query = "Đi food tour, ưu tiên món nướng và món lạnh", Style = "FOOD_TOUR" }
    };
    var results = new List<object>();
    foreach (var scenario in scenarios)
    {
        capture.Clear();
        MealPlanV2Response created;
        await using (var createDb = new SNMDbContext(options))
        {
            created = (await RuntimeMealService(createDb, capture).CreateAsync(customerId, new CreateMealPlanV2Request
            {
                PartySize = scenario.Party, Budget = scenario.Budget, DiningStyle = scenario.Style,
                NaturalLanguageRequest = scenario.Query, RequestedPlanCount = 3,
                IdempotencyKey = $"runtime-meal-v2-{scenario.Code}-{Guid.NewGuid():N}"
            }, default)).Data!;
        }
        var details = new List<object>();
        foreach (var summary in created.Plans)
        {
            await using var detailDb = new SNMDbContext(options);
            var detail = (await RuntimeMealService(detailDb, capture).GetDetailAsync(customerId, summary.PlanId, default)).Data!;
            details.Add(new
            {
                summary.PlanCode, summary.Strategy, summary.Title, Market = summary.Market.Name,
                summary.TotalPrice, summary.RemainingBudget, summary.BudgetUtilizationPercent,
                summary.ServingCoverage, summary.CourseCoverage, summary.CompatibilityScore, summary.CompatibilityLabel,
                Foods = detail.CourseGroups.SelectMany(group => group.Items).Select(item => new
                {
                    item.FoodId, item.FoodName, item.Course, item.Quantity, item.ServingCountSnapshot,
                    Booth = item.Booth.Name, item.TotalPriceSnapshot, item.CompatibilityScore, item.Reason
                    , Ingredients = candidates.Single(candidate => candidate.FoodId == item.FoodId).IngredientCodes
                }).ToArray()
            });
        }
        object? mutationProof = null;
        if (scenario.Code == "A" && created.Plans.Count > 0)
        {
            var target = created.Plans.First();
            await using var readDb = new SNMDbContext(options);
            var readService = RuntimeMealService(readDb, capture);
            var before = (await readService.GetDetailAsync(customerId, target.PlanId, default)).Data!;
            var main = before.CourseGroups.SelectMany(group => group.Items).First(item => item.Course == "MAIN_COURSE");
            var alternatives = (await readService.GetAlternativesAsync(customerId, target.PlanId, main.PlanItemId, 1, 10, default)).Data!;
            if (alternatives.Items.Count > 0)
            {
                await using var replaceDb = new SNMDbContext(options);
                var replaceService = RuntimeMealService(replaceDb, capture);
                var replaced = (await replaceService.ReplaceAsync(customerId, target.PlanId, main.PlanItemId, new ReplaceMealPlanItemRequest
                {
                    ReplacementFoodId = alternatives.Items.First().FoodId, ExpectedPlanVersion = before.Version
                }, default)).Data!;
                var replacedMain = replaced.CourseGroups.SelectMany(group => group.Items).First(item => item.Course == "MAIN_COURSE");
                await using var regenerateDb = new SNMDbContext(options);
                var regenerateService = RuntimeMealService(regenerateDb, capture);
                var regenerated = (await regenerateService.RegenerateCourseAsync(customerId, target.PlanId, DomainLayer.Enums.FoodCourse.MAIN_COURSE,
                    new RegenerateMealPlanCourseRequest { ExpectedPlanVersion = replaced.Version }, default)).Data!;
                var regeneratedMain = regenerated.CourseGroups.SelectMany(group => group.Items).First(item => item.Course == "MAIN_COURSE");
                mutationProof = new { AlternativeCount = alternatives.Total, BeforeVersion = before.Version, BeforeFoodId = main.FoodId,
                    ReplacedVersion = replaced.Version, ReplacedFoodId = replacedMain.FoodId,
                    RegeneratedVersion = regenerated.Version, RegeneratedFoodId = regeneratedMain.FoodId };
            }
        }
        var persistedIntent = await db.AiMealPlanSessions.AsNoTracking().Where(value => value.Id == created.SessionId)
            .Select(value => value.ParsedPreferenceJson).SingleAsync();
        results.Add(new { scenario.Code, scenario.Party, scenario.Budget, scenario.Query, ParsedIntent = JsonDocument.Parse(persistedIntent ?? "{}").RootElement.Clone(),
            created.Provider, created.UsedProviderFallback, created.RequestedPlanCount, created.GeneratedPlanCount,
            created.Limitations, Plans = details, MutationProof = mutationProof, Diagnostics = capture.Messages.ToArray() });
    }
    mealPlanRuntime = results.ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Database = connection.Database, Foods = foods.Count, CandidateRows = candidates.Count,
        GeneratedAtUtc = now, MealPlanRuntime = mealPlanRuntime
    }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var report = new
{
    Database = connection.Database,
    GeneratedAtUtc = now,
    VietnamLocalTime = local.ToString("HH:mm:ss"),
    AppliedMigrations = applied,
    PendingMigrations = pending,
    Counts = new
    {
        NightMarkets = await db.NightMarkets.CountAsync(),
        Booths = await db.Booths.CountAsync(),
        Foods = foods.Count,
        FoodNameContainsPho = phoRows.Length,
        FoodActiveAvailable = foods.Count(value => !value.IsDeleted && value.IsAvailable),
        CurrentEffectivePricePositive = foods.Count(value => FoodPriceResolver.GetCurrentPrice(value, now) > 0),
        BoothActive = foods.Count(value => value.Booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Active),
        MarketActive = foods.Count(value => !value.Booth.NightMarket.IsDeleted
            && value.Booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Active
            && value.Booth.NightMarket.ModerationStatus == DomainLayer.Enums.GeneralEnum.ModerationStatus.Active),
        OpenNow = foods.Count(value => CustomerAvailability.IsOpenNow(true, value.Booth.NightMarket.OpeningHours,
            value.Booth.NightMarket.ClosingHours, value.Booth.OpenTime, value.Booth.CloseTime, local)),
        IngredientRelations = await db.FoodItemIngredients.CountAsync(),
        PreparationRelations = await db.FoodItemPreparationMethods.CountAsync(),
        TasteRelations = await db.FoodItemTasteProfiles.CountAsync(),
        CourseRelations = await db.FoodItemCourses.CountAsync(),
        DiningPurposeRelations = await db.FoodItemDiningPurposes.CountAsync(),
        FoodAiProfiles = await db.FoodAiProfiles.CountAsync(),
        Orders = await db.Orders.CountAsync(),
        Payments = await db.Payments.CountAsync(),
        Carts = await db.Carts.CountAsync(),
        CartItems = await db.CartItems.CountAsync(),
        Reviews = await db.Reviews.CountAsync(),
        CuratedFoods = await db.FoodItems.CountAsync(value => value.Id.ToString().StartsWith("a2650000")),
        CuratedFoodsMissingImages = await db.FoodItems.CountAsync(value => value.Id.ToString().StartsWith("a2650000") &&
            (value.ThumbnailUrl == null || !value.FoodImages.Any())),
        CuratedFoodsMissingProfiles = await db.FoodItems.CountAsync(value => value.Id.ToString().StartsWith("a2650000") && value.AiProfile == null),
        CuratedFoodsMissingCourses = await db.FoodItems.CountAsync(value => value.Id.ToString().StartsWith("a2650000") && !value.Courses.Any()),
        CuratedFoodsMissingIngredients = await db.FoodItems.CountAsync(value => value.Id.ToString().StartsWith("a2650000") && !value.Ingredients.Any())
    },
    FoodAiProfileStatuses = profileStatusRows.Select(value => new { Status = value.Status.ToString(), value.Count })
        .OrderBy(value => value.Status).ToArray(),
    MealPlanRuntime = mealPlanRuntime,
    PhoFoods = phoRows.Select(food =>
    {
        var candidate = candidates.Single(value => value.FoodId == food.Id);
        var semantic = matcher.Match(intent, candidate);
        var ranked = ranker.Rank(intent, candidate, semantic, null);
        return new
        {
            food.Id, food.Name, food.Description, food.IsAvailable, food.IsDeleted,
            CurrentEffectivePrice = FoodPriceResolver.GetCurrentPrice(food, now),
            BoothStatus = food.Booth.Status.ToString(), food.Booth.OpenTime, food.Booth.CloseTime,
            MarketStatus = food.Booth.NightMarket.Status.ToString(),
            MarketModerationStatus = food.Booth.NightMarket.ModerationStatus.ToString(),
            MarketIsDeleted = food.Booth.NightMarket.IsDeleted, food.Booth.NightMarket.OpeningHours, food.Booth.NightMarket.ClosingHours,
            IsOpenNow = CustomerAvailability.IsOpenNow(true, food.Booth.NightMarket.OpeningHours,
                food.Booth.NightMarket.ClosingHours, food.Booth.OpenTime, food.Booth.CloseTime, local),
            food.SpiceLevel, food.ServingTemperature, food.EstimatedServingCount, food.IsShareable,
            Courses = food.Courses.Select(value => value.Course.ToString()).ToArray(),
            DiningPurposes = food.DiningPurposes.Select(value => value.Purpose.ToString()).ToArray(),
            SearchText = food.AiProfile?.SearchText, AiProfileStatus = food.AiProfile?.Status.ToString(),
            ProjectedFoodName = candidate.FoodName, ProjectedDescription = candidate.Description,
            ProjectedSearchText = candidate.SearchText, ProjectedIngredientCodes = candidate.IngredientCodes,
            SemanticScore = semantic.Score, RecommendationScore = ranked.BaseScore, Tier = ranked.Tier.ToString()
        };
    }).ToArray()
};

Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

string? Value(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

MealPlanV2Service RuntimeMealService(SNMDbContext context, ILogger<MealPlanV2Service> runtimeLogger) => new(
    new DeterministicExtractor(), new MealPlanCandidateRepository(new FoodRecommendationReadRepository(context)),
    new MealPlanV2Repository(context), new FoodSemanticMetadataRepository(context), new MealPlanPolicyResolver(),
    new MealPlanRecalculationService(), new UnusedCart(), Options.Create(new MealPlanV2Options()), TimeProvider.System, runtimeLogger);

FoodRecommendationV2Service RuntimeRecommendationService(SNMDbContext context)
{
    var recommendationOptions = Options.Create(new RecommendationV2Options { MaximumExplanationCalls = 0 });
    return new(new DeterministicExtractor(), new NoExplanation(), new DeterministicRecommendationReasonBuilder(),
        new FoodRecommendationIntentNormalizer(recommendationOptions), new DeterministicFoodSemanticMatcher(),
        new FoodRecommendationRanker(recommendationOptions), new FoodRecommendationDiversityReranker(recommendationOptions),
        new FoodRecommendationReadRepository(context), new AiRecommendationSessionRepository(context),
        new ProfilelessMetadata(new FoodSemanticMetadataRepository(context)), recommendationOptions, TimeProvider.System, new DiagnosticsHost(),
        new CaptureLogger<FoodRecommendationV2Service>());
}

sealed class DeterministicExtractor : IAiIntentExtractor
{
    private readonly DeterministicFoodIntentParser parser = new();
    public Task<MealPlanIntentExtractionResult> ExtractMealPlanIntentAsync(MealPlanIntentRequest request, CancellationToken cancellationToken)
        => Task.FromResult(parser.Parse(request));
    public Task<FoodRecommendationIntentExtractionResult> ExtractFoodRecommendationIntentAsync(FoodRecommendationIntentRequest request, CancellationToken cancellationToken)
        => Task.FromResult(parser.Parse(request));
}

sealed class UnusedCart : IMealPlanCartIntegrationService
{
    public Task<CartBatchAddResponse> AddItemsAsync(Guid customerId, IReadOnlyCollection<(Guid FoodItemId, int Quantity)> items, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

sealed class NoExplanation : IAiExplanationGenerator
{
    public Task<AiGeneratedTextResult> GenerateFoodRecommendationReasonAsync(FoodRecommendationExplanationContext context, CancellationToken cancellationToken)
        => Task.FromResult(new AiGeneratedTextResult { IsSuccess = false, UsedFallback = true, FailureCategory = AiProviderFailureCategory.DISABLED });
}

sealed class DiagnosticsHost : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "AiV2RuntimeDiagnosticsTool";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

sealed class ProfilelessMetadata(IFoodSemanticMetadataRepository inner) : IFoodSemanticMetadataRepository
{
    public Task<FoodSemanticCatalogSet> GetActiveCatalogsAsync(CancellationToken cancellationToken = default) => inner.GetActiveCatalogsAsync(cancellationToken);
    public Task<IReadOnlyCollection<Ingredient>> GetActiveIngredientsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => inner.GetActiveIngredientsAsync(ids, cancellationToken);
    public Task<IReadOnlyCollection<Allergen>> GetActiveAllergensAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => inner.GetActiveAllergensAsync(ids, cancellationToken);
    public Task<IReadOnlyCollection<DietaryAttribute>> GetActiveDietaryAttributesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => inner.GetActiveDietaryAttributesAsync(ids, cancellationToken);
    public Task<IReadOnlyCollection<PreparationMethod>> GetActivePreparationMethodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => inner.GetActivePreparationMethodsAsync(ids, cancellationToken);
    public Task<IReadOnlyCollection<TasteProfile>> GetActiveTasteProfilesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => inner.GetActiveTasteProfilesAsync(ids, cancellationToken);
    public Task<CustomerFoodProfile?> GetCustomerProfileAsync(Guid customerId, bool tracking, CancellationToken cancellationToken = default) => Task.FromResult<CustomerFoodProfile?>(null);
    public void AddCustomerProfile(CustomerFoodProfile profile) => throw new NotSupportedException();
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class CaptureLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];
    public void Clear() => Messages.Clear();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}
