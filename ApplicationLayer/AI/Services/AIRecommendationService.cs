using System.Text.Json;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.AI.Services;

public class AIRecommendationService : IAIRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IFoodItemRepository _foodItems;
    private readonly IFoodTagRepository _foodTags;
    private readonly ICustomerPreferenceRepository _preferences;
    private readonly IAIRecommendationLogRepository _logs;
    private readonly IAIProviderService _aiProvider;

    public AIRecommendationService(
        IFoodItemRepository foodItems,
        IFoodTagRepository foodTags,
        ICustomerPreferenceRepository preferences,
        IAIRecommendationLogRepository logs,
        IAIProviderService aiProvider)
    {
        _foodItems = foodItems;
        _foodTags = foodTags;
        _preferences = preferences;
        _logs = logs;
        _aiProvider = aiProvider;
    }

    public async Task<ApiResponse<AIHomeResponse>> GetHomeAsync(
        Guid? customerId,
        CancellationToken cancellationToken = default)
    {
        var tags = await _foodTags.GetActiveAsync(cancellationToken);
        var preferredCodes = new[] { "SPICY", "GRILLED", "DRINK", "SNACK", "FULLMEAL", "VIETNAMESE", "DESSERT", "BUDGET" };
        var popularTags = tags
            .OrderBy(tag =>
            {
                var index = Array.IndexOf(preferredCodes, tag.Code.ToUpperInvariant());
                return index < 0 ? int.MaxValue : index;
            })
            .ThenBy(tag => tag.Name)
            .Take(10)
            .Select(tag => new AIHomeTagResponse
            {
                Id = tag.Id,
                Code = tag.Code,
                DisplayName = string.IsNullOrWhiteSpace(tag.Description) ? tag.Name : tag.Description,
                Group = tag.TagGroup.ToString()
            })
            .ToList();

        var response = new AIHomeResponse
        {
            QuickPrompts =
            [
                new()
                {
                    Label = "Món cay dưới 100k",
                    Query = "Tôi muốn món cay, nóng, dưới 100k",
                    Intent = "FoodDiscovery",
                    Budget = 100000
                },
                new()
                {
                    Label = "Đi 4 người 300k",
                    Query = "Tôi đi 4 người, thích đồ nướng và nước uống mát",
                    Intent = "DiningPlan",
                    GroupSize = 4,
                    Budget = 300000
                },
                new()
                {
                    Label = "Ăn nhẹ dễ ăn",
                    Query = "Tôi muốn ăn nhẹ, dễ ăn, không quá cay",
                    Intent = "FoodDiscovery",
                    Budget = 120000
                },
                new()
                {
                    Label = "Food tour nhiều món",
                    Query = "Gợi ý food tour nhiều món cho nhóm bạn",
                    Intent = "DiningPlan",
                    GroupSize = 3,
                    Budget = 350000
                }
            ],
            PopularTags = popularTags,
            DiningStyles = ["FullMeal", "LightMeal", "FoodTour", "DateNight", "Family"]
        };

        return ApiResponse<AIHomeResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<FoodDiscoveryResponse>> FoodDiscoveryAsync(
        Guid? customerId,
        FoodDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Limit = Math.Clamp(request.Limit, 1, 50);
        var tags = await _foodTags.GetActiveAsync(cancellationToken);
        var intent = await BuildIntentAsync(
            request.Query,
            tags,
            request.SelectedTagIds,
            [],
            customerId,
            request.BudgetMax,
            null,
            cancellationToken);

        var candidates = await _foodItems.GetAiCandidatesAsync(request.NightMarketId, cancellationToken);
        var scored = candidates
            .Select(item => ToDiscoveryItem(item, intent, request.SortBy))
            .Where(item => item.MatchScore > 0)
            .ToList();

        scored = request.SortBy.Trim().ToLowerInvariant() switch
        {
            "pricelowtohigh" => scored.OrderBy(item => item.Price).ThenByDescending(item => item.MatchScore).ToList(),
            "rating" => scored.OrderByDescending(item => item.Rating).ThenByDescending(item => item.MatchScore).ToList(),
            _ => scored.OrderByDescending(item => item.MatchScore).ThenByDescending(item => item.Rating).ThenBy(item => item.Price).ToList()
        };

        var response = new FoodDiscoveryResponse
        {
            ParsedIntent = ToParsedIntent(intent),
            Results = scored.Take(request.Limit).ToList()
        };

        var log = await TrySaveLogAsync(
            customerId,
            request.NightMarketId,
            AIRecommendationType.FoodDiscovery,
            request,
            response.ParsedIntent,
            response,
            cancellationToken);

        if (log is not null)
        {
            response.LogId = log.Id;
            log.ResultJson = JsonSerializer.Serialize(response, JsonOptions);
            _logs.Update(log);
            await _logs.SaveChangesAsync();
        }

        return ApiResponse<FoodDiscoveryResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<FoodDiscoveryResponse>> GetPersonalizedRecommendationsAsync(
        Guid? customerId,
        CancellationToken cancellationToken = default)
    {
        var request = new FoodDiscoveryRequest
        {
            Query = customerId.HasValue
                ? "Gợi ý món phù hợp với khẩu vị đã lưu của tôi"
                : "Gợi ý món ngon phổ biến, dễ ăn",
            Limit = 12,
            SortBy = "BestMatch"
        };

        return await FoodDiscoveryAsync(customerId, request, cancellationToken);
    }

    public async Task<ApiResponse<DiningPlanAssistantResponse>> DiningPlanAssistantAsync(
        Guid? customerId,
        DiningPlanAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Budget <= 0)
            throw AppException.BadRequest("Vui lòng chọn ngân sách để AI tạo kế hoạch phù hợp.");

        var tags = await _foodTags.GetActiveAsync(cancellationToken);
        var intent = await BuildIntentAsync(
            request.Query,
            tags,
            request.PreferredTagIds,
            request.AvoidTagIds,
            customerId,
            request.Budget,
            request.DiningStyle,
            cancellationToken);

        var candidates = await _foodItems.GetAiCandidatesAsync(request.NightMarketId, cancellationToken);
        var options = BuildPlanOptions(candidates, intent, request)
            .OrderByDescending(option => option.MatchScore)
            .ThenBy(option => option.EstimatedTotal)
            .Take(3)
            .ToList();

        var response = new DiningPlanAssistantResponse
        {
            Step = options.Count == 0 ? "NO_PLAN_FOUND" : "PLAN_OPTIONS",
            Message = options.Count == 0
                ? "Chưa tìm thấy phương án phù hợp. Bạn thử tăng ngân sách hoặc bỏ bớt món cần tránh nhé."
                : "AI đã chuẩn bị các phương án ăn uống dễ chọn cho bạn.",
            Options = options
        };

        var log = await TrySaveLogAsync(
            customerId,
            request.NightMarketId,
            AIRecommendationType.DiningPlan,
            request,
            ToParsedIntent(intent),
            response,
            cancellationToken);

        if (log is not null)
        {
            response.LogId = log.Id;
            log.ResultJson = JsonSerializer.Serialize(response, JsonOptions);
            _logs.Update(log);
            await _logs.SaveChangesAsync();
        }

        return ApiResponse<DiningPlanAssistantResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<DiningPlanReadyResponse>> ConfirmDiningPlanAsync(
        Guid? customerId,
        ConfirmDiningPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var log = await GetOwnedLogAsync(customerId, request.LogId, cancellationToken);
        var result = JsonSerializer.Deserialize<DiningPlanAssistantResponse>(log.ResultJson, JsonOptions)
            ?? throw AppException.BadRequest("AI log result is invalid.");
        var option = result.Options.FirstOrDefault(item => item.OptionId == request.OptionId)
            ?? throw AppException.NotFound("Dining plan option was not found.");

        var candidates = await _foodItems.GetAiCandidatesAsync(option.NightMarketId, cancellationToken);
        var candidateMap = candidates.ToDictionary(item => item.Id);
        foreach (var item in option.PlanPreview)
        {
            if (!candidateMap.TryGetValue(item.FoodItemId, out var current))
                throw AppException.Conflict("A food item in this plan is no longer available.");
            if (current.Price != item.UnitPrice)
                throw AppException.Conflict("A food item price has changed. Please regenerate the plan.");
        }

        log.SelectedOptionId = option.OptionId;
        log.UpdatedAt = DateTime.UtcNow;
        _logs.Update(log);
        await _logs.SaveChangesAsync();

        var response = new DiningPlanReadyResponse
        {
            NightMarketId = option.NightMarketId,
            NightMarketName = option.NightMarketName,
            GroupSize = option.PlanPreview.Max(item => item.Quantity),
            Budget = option.Budget,
            EstimatedTotal = option.EstimatedTotal,
            PlanItems = option.PlanPreview,
            Reason = option.Reason
        };

        return ApiResponse<DiningPlanReadyResponse>.SuccessResponse(response, "Dining plan confirmed successfully.");
    }

    public async Task<ApiResponse<DiningPlanAssistantResponse>> RegenerateDiningPlanAsync(
        Guid? customerId,
        RegenerateDiningPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var log = await GetOwnedLogAsync(customerId, request.LogId, cancellationToken);
        var original = JsonSerializer.Deserialize<DiningPlanAssistantResponse>(log.ResultJson, JsonOptions)
            ?? throw AppException.BadRequest("AI log result is invalid.");

        var options = request.Priority.Trim().ToLowerInvariant() switch
        {
            "cheaper" => original.Options.OrderBy(option => option.EstimatedTotal).ToList(),
            "higherrated" => original.Options.OrderByDescending(option => option.MatchScore).ToList(),
            _ => original.Options.OrderByDescending(option => option.MatchScore).ThenBy(option => option.EstimatedTotal).ToList()
        };

        original.Options = options;
        original.Message = "AI đã sắp xếp lại các phương án ăn uống.";
        log.ResultJson = JsonSerializer.Serialize(original, JsonOptions);
        log.UpdatedAt = DateTime.UtcNow;
        _logs.Update(log);
        await _logs.SaveChangesAsync();

        return ApiResponse<DiningPlanAssistantResponse>.SuccessResponse(original);
    }

    public async Task<ApiResponse<AIFeedbackResponse>> SubmitFeedbackAsync(
        Guid? customerId,
        AIFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LogId.HasValue)
        {
            await GetOwnedLogAsync(customerId, request.LogId.Value, cancellationToken);
        }

        var feedback = new
        {
            request.LogId,
            request.OptionId,
            request.FoodItemId,
            request.FeedbackType,
            request.Reason
        };

        var log = await TrySaveLogAsync(
            customerId,
            null,
            AIRecommendationType.PreferenceProfile,
            feedback,
            null,
            new { Status = "Recorded", request.FeedbackType },
            cancellationToken);

        return ApiResponse<AIFeedbackResponse>.SuccessResponse(new AIFeedbackResponse
        {
            FeedbackLogId = log?.Id ?? Guid.Empty,
            Message = "Cảm ơn bạn, AI sẽ dùng phản hồi này để điều chỉnh gợi ý lần sau."
        });
    }

    public async Task<ApiResponse<AIRecommendationLogResponse>> GetLogAsync(
        Guid logId,
        CancellationToken cancellationToken = default)
    {
        var log = await _logs.GetByIdAsync(logId)
            ?? throw AppException.NotFound("AI recommendation log was not found.");

        return ApiResponse<AIRecommendationLogResponse>.SuccessResponse(new AIRecommendationLogResponse
        {
            Id = log.Id,
            CustomerId = log.CustomerId,
            NightMarketId = log.NightMarketId,
            RecommendationType = log.RecommendationType.ToString(),
            InputJson = log.InputJson,
            ParsedIntentJson = log.ParsedIntentJson,
            ResultJson = log.ResultJson,
            SelectedOptionId = log.SelectedOptionId,
            CreatedAt = log.CreatedAt
        });
    }

    private async Task<ResolvedIntent> BuildIntentAsync(
        string? query,
        IReadOnlyCollection<FoodTag> allowedTags,
        IReadOnlyCollection<Guid> preferredTagIds,
        IReadOnlyCollection<Guid> avoidTagIds,
        Guid? customerId,
        decimal? budget,
        string? diningStyle,
        CancellationToken cancellationToken)
    {
        FoodIntentDto providerIntent;
        try
        {
            providerIntent = await _aiProvider.ParseFoodIntentAsync(
                query,
                allowedTags.Select(tag => tag.Code).ToList(),
                cancellationToken);
        }
        catch
        {
            providerIntent = new FoodIntentDto();
        }

        var preferred = preferredTagIds.ToHashSet();
        var avoid = avoidTagIds.ToHashSet();
        foreach (var tagName in providerIntent.MatchedTagNames)
        {
            var tag = FindTag(allowedTags, tagName);
            if (tag is not null) preferred.Add(tag.Id);
        }
        foreach (var tagName in providerIntent.AvoidTagNames)
        {
            var tag = FindTag(allowedTags, tagName);
            if (tag is not null) avoid.Add(tag.Id);
        }

        if (customerId.HasValue)
        {
            var preferences = await _preferences.GetByCustomerAsync(customerId.Value, cancellationToken);
            foreach (var preference in preferences)
            {
                if (preference.PreferenceKind == CustomerPreferenceKind.Like)
                    preferred.Add(preference.FoodTagId);
                if (preference.PreferenceKind == CustomerPreferenceKind.Avoid)
                    avoid.Add(preference.FoodTagId);
            }
        }

        foreach (var tagId in avoid)
        {
            preferred.Remove(tagId);
        }

        return new ResolvedIntent(
            preferred,
            avoid,
            providerIntent.BudgetMax ?? budget,
            providerIntent.DiningStyle ?? diningStyle,
            allowedTags.ToDictionary(tag => tag.Id));
    }

    private FoodDiscoveryItemResponse ToDiscoveryItem(
        FoodItem item,
        ResolvedIntent intent,
        string sortBy)
    {
        var itemTagIds = item.FoodItemTags.Select(tag => tag.FoodTagId).ToHashSet();
        if (intent.AvoidTagIds.Overlaps(itemTagIds))
        {
            return new FoodDiscoveryItemResponse { MatchScore = 0 };
        }

        var matchedCount = intent.PreferredTagIds.Count == 0
            ? 1
            : intent.PreferredTagIds.Count(itemTagIds.Contains);
        var score = intent.PreferredTagIds.Count == 0
            ? 55
            : (int)Math.Round(matchedCount * 70.0 / intent.PreferredTagIds.Count);

        if (intent.BudgetMax.HasValue && item.Price <= intent.BudgetMax.Value) score += 10;
        if (item.IsFeatured) score += 5;
        score += (int)Math.Min(10, Math.Round((item.Booth.AverageRating ?? 0) * 2));

        var matchedTags = item.FoodItemTags
            .Where(tag => intent.PreferredTagIds.Contains(tag.FoodTagId))
            .Select(tag => tag.FoodTag.Name)
            .ToList();

        return new FoodDiscoveryItemResponse
        {
            FoodItemId = item.Id,
            FoodName = item.Name,
            ImageUrl = item.ThumbnailUrl,
            Price = item.Price,
            BoothId = item.BoothId,
            BoothName = item.Booth.BoothName,
            NightMarketId = item.Booth.NightMarketId,
            NightMarketName = item.Booth.NightMarket.Name,
            ZoneName = item.Booth.Zone?.ZoneName,
            BoothSlotCode = item.Booth.SlotNumber ?? item.Booth.BoothCode,
            Rating = item.Booth.AverageRating ?? 0,
            MatchScore = Math.Clamp(score, 0, 100),
            Reason = BuildReason(item.Name, matchedTags, item.Price, intent.BudgetMax, item.Booth.AverageRating, item.Booth.NightMarket.Name),
            Tags = item.FoodItemTags.Select(tag => tag.FoodTag.Code).ToList()
        };
    }

    private IReadOnlyCollection<DiningPlanOptionResponse> BuildPlanOptions(
        IReadOnlyCollection<FoodItem> candidates,
        ResolvedIntent intent,
        DiningPlanAssistantRequest request)
        => candidates
            .GroupBy(item => item.Booth.NightMarketId)
            .Select((marketGroup, index) => BuildPlanOption(marketGroup.ToList(), intent, request, index + 1))
            .Where(option => option is not null)
            .Select(option => option!)
            .ToList();

    private DiningPlanOptionResponse? BuildPlanOption(
        IReadOnlyCollection<FoodItem> marketItems,
        ResolvedIntent intent,
        DiningPlanAssistantRequest request,
        int index)
    {
        var filtered = marketItems
            .Where(item => !intent.AvoidTagIds.Overlaps(item.FoodItemTags.Select(tag => tag.FoodTagId).ToHashSet()))
            .OrderByDescending(item => ScoreItem(item, intent))
            .ThenBy(item => item.Price)
            .ToList();

        if (filtered.Count == 0) return null;

        var planItems = new List<DiningPlanItemResponse>();
        TryAddRole(planItems, filtered, "MainDish", request.GroupSize, ["FULLMEAL", "RICE", "NOODLE", "BEEF", "CHICKEN", "PORK"]);
        TryAddRole(planItems, filtered, "Drink", request.GroupSize, ["DRINK", "COLD"]);
        TryAddRole(planItems, filtered, "Snack", Math.Max(1, request.GroupSize / 2), ["SNACK", "SHAREABLE", "FRIED", "GRILLED"]);

        if (planItems.Count == 0)
        {
            var first = filtered.First();
            planItems.Add(ToPlanItem(first, request.GroupSize, "MainDish"));
        }

        var total = planItems.Sum(item => item.TotalPrice);
        var feasibilityStatus = "ExactMatch";
        if (total > request.Budget)
        {
            planItems = planItems.OrderBy(item => item.TotalPrice).Take(2).ToList();
            total = planItems.Sum(item => item.TotalPrice);
        }

        if (planItems.Count == 0) return null;
        if (total > request.Budget)
        {
            feasibilityStatus = "NearBudget";
        }

        var market = filtered.First().Booth.NightMarket;
        var score = (int)Math.Round(planItems.Average(item =>
            ScoreItem(filtered.First(food => food.Id == item.FoodItemId), intent)));

        return new DiningPlanOptionResponse
        {
            OptionId = $"OPT{index:000}",
            OptionType = index switch
            {
                1 => "BestMatch",
                2 => "BudgetFriendly",
                3 => "HighRating",
                _ => "Alternative"
            },
            FeasibilityStatus = feasibilityStatus,
            Label = index switch
            {
                1 => "Phù hợp nhất",
                2 => "Tiết kiệm hơn",
                3 => "Rating cao hơn",
                _ => $"Phương án {index}"
            },
            NightMarketId = market.Id,
            NightMarketName = market.Name,
            MatchScore = Math.Clamp(score, 0, 100),
            EstimatedTotal = total,
            Budget = request.Budget,
            PlanPreview = planItems,
            Reason = total <= request.Budget
                ? $"Có món thật đang bán tại {market.Name}, tổng dự kiến {total:N0} trong ngân sách {request.Budget:N0}."
                : $"Gần phù hợp nhất tại {market.Name}, nhưng vượt ngân sách khoảng {(total - request.Budget):N0}."
        };
    }

    private static void TryAddRole(
        ICollection<DiningPlanItemResponse> planItems,
        IReadOnlyCollection<FoodItem> candidates,
        string role,
        int quantity,
        IReadOnlyCollection<string> tagCodes)
    {
        var usedIds = planItems.Select(item => item.FoodItemId).ToHashSet();
        var item = candidates.FirstOrDefault(food =>
            !usedIds.Contains(food.Id)
            && food.FoodItemTags.Any(tag => tagCodes.Contains(tag.FoodTag.Code)));
        if (item is not null)
        {
            planItems.Add(ToPlanItem(item, quantity, role));
        }
    }

    private static DiningPlanItemResponse ToPlanItem(FoodItem item, int quantity, string role)
        => new()
        {
            FoodItemId = item.Id,
            FoodName = item.Name,
            BoothId = item.BoothId,
            BoothName = item.Booth.BoothName,
            BoothSlotCode = item.Booth.SlotNumber ?? item.Booth.BoothCode,
            ZoneName = item.Booth.Zone?.ZoneName,
            Quantity = quantity,
            UnitPrice = item.Price,
            TotalPrice = item.Price * quantity,
            Role = role
        };

    private double ScoreItem(FoodItem item, ResolvedIntent intent)
    {
        var tagIds = item.FoodItemTags.Select(tag => tag.FoodTagId).ToHashSet();
        var matched = intent.PreferredTagIds.Count == 0
            ? 1
            : intent.PreferredTagIds.Count(tagIds.Contains);
        var score = intent.PreferredTagIds.Count == 0 ? 55 : matched * 75.0 / intent.PreferredTagIds.Count;
        if (intent.BudgetMax.HasValue && item.Price <= intent.BudgetMax.Value) score += 10;
        score += (double)(item.Booth.AverageRating ?? 0) * 2;
        return Math.Clamp(score, 0, 100);
    }

    private async Task<AIRecommendationLog> SaveLogAsync(
        Guid? customerId,
        Guid? nightMarketId,
        AIRecommendationType type,
        object input,
        object? parsedIntent,
        object result,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var log = new AIRecommendationLog
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            NightMarketId = nightMarketId,
            RecommendationType = type,
            InputJson = JsonSerializer.Serialize(input, JsonOptions),
            ParsedIntentJson = parsedIntent is null ? null : JsonSerializer.Serialize(parsedIntent, JsonOptions),
            ResultJson = JsonSerializer.Serialize(result, JsonOptions),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _logs.AddAsync(log);
        await _logs.SaveChangesAsync();
        return log;
    }

    private async Task<AIRecommendationLog?> TrySaveLogAsync(
        Guid? customerId,
        Guid? nightMarketId,
        AIRecommendationType type,
        object input,
        object? parsedIntent,
        object result,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SaveLogAsync(customerId, nightMarketId, type, input, parsedIntent, result, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<AIRecommendationLog> GetOwnedLogAsync(
        Guid? customerId,
        Guid logId,
        CancellationToken cancellationToken)
    {
        var log = await _logs.GetByIdAsync(logId)
            ?? throw AppException.NotFound("AI recommendation log was not found.");
        if (log.CustomerId.HasValue && customerId.HasValue && log.CustomerId != customerId)
            throw AppException.Forbidden("You do not have permission to access this AI log.");
        return log;
    }

    private static FoodTag? FindTag(IReadOnlyCollection<FoodTag> tags, string tagName)
        => tags.FirstOrDefault(tag =>
            tag.Code.Equals(tagName, StringComparison.OrdinalIgnoreCase)
            || tag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));

    private static ParsedFoodIntentResponse ToParsedIntent(ResolvedIntent intent)
        => new()
        {
            MatchedTags = intent.PreferredTagIds
                .Where(intent.TagMap.ContainsKey)
                .Select(tagId => intent.TagMap[tagId].Code)
                .ToList(),
            AvoidTags = intent.AvoidTagIds
                .Where(intent.TagMap.ContainsKey)
                .Select(tagId => intent.TagMap[tagId].Code)
                .ToList(),
            BudgetMax = intent.BudgetMax,
            DiningStyle = intent.DiningStyle
        };

    private static string BuildReason(
        string foodName,
        IReadOnlyCollection<string> matchedTags,
        decimal price,
        decimal? budget,
        decimal? rating,
        string marketName)
    {
        var tagText = matchedTags.Count == 0 ? "nhu cầu bạn nhập" : string.Join(", ", matchedTags);
        var budgetText = budget.HasValue ? $", nằm trong ngân sách {budget.Value:N0}" : string.Empty;
        return $"{foodName} phù hợp với {tagText}, giá {price:N0}{budgetText}, gian hàng đạt {(rating ?? 0):0.0} sao tại {marketName}.";
    }

    private sealed record ResolvedIntent(
        HashSet<Guid> PreferredTagIds,
        HashSet<Guid> AvoidTagIds,
        decimal? BudgetMax,
        string? DiningStyle,
        IReadOnlyDictionary<Guid, FoodTag> TagMap);
}
