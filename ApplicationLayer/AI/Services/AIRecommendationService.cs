using System.Text.Json;
using ApplicationLayer.AI;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.NightMarkets;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Options;
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
    private readonly IAICustomerContextRepository _customerContext;
    private readonly AIProviderSettings _settings;
    private readonly TimeProvider _timeProvider;

    public AIRecommendationService(
        IFoodItemRepository foodItems,
        IFoodTagRepository foodTags,
        ICustomerPreferenceRepository preferences,
        IAIRecommendationLogRepository logs,
        IAIProviderService aiProvider,
        IAICustomerContextRepository customerContext,
        IOptions<AIProviderSettings> settings,
        TimeProvider timeProvider)
    {
        _foodItems = foodItems;
        _foodTags = foodTags;
        _preferences = preferences;
        _logs = logs;
        _aiProvider = aiProvider;
        _customerContext = customerContext;
        _settings = settings.Value;
        _timeProvider = timeProvider;
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
        request.SelectedTagIds ??= [];
        request.SortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "BestMatch" : request.SortBy;
        ValidateDiscoveryRequest(request);
        request.Limit = Math.Clamp(request.Limit, 1, 3);
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

        var candidates = await _foodItems.GetAiCandidatesAsync(request.NightMarketId, GetCandidateLimit(), cancellationToken);
        ApplyEffectivePrices(candidates);
        var marketDistances = candidates
            .GroupBy(item => item.Booth.NightMarketId)
            .ToDictionary(
                group => group.Key,
                group => CalculateDistanceMeters(
                    request.PreferNearMe,
                    request.NightMarketId.HasValue,
                    request.Latitude,
                    request.Longitude,
                    group.First().Booth.NightMarket));
        var scored = candidates
            .Where(item => !request.NightMarketId.HasValue || item.Booth.NightMarketId == request.NightMarketId.Value)
            .Where(item => !intent.BudgetMax.HasValue || item.Price <= intent.BudgetMax.Value)
            .Select(item => ToDiscoveryItem(item, intent, marketDistances))
            .Where(item => item.MatchScore > 0)
            .ToList();

        scored = request.SortBy.Trim().ToLowerInvariant() switch
        {
            "pricelowtohigh" => scored.OrderBy(item => item.Price).ThenByDescending(item => item.MatchScore).ToList(),
            "rating" => scored.OrderByDescending(item => item.BoothRating).ThenByDescending(item => item.MatchScore).ToList(),
            _ => scored.OrderByDescending(item => item.MatchScore).ThenByDescending(item => item.BoothRating).ThenBy(item => item.Price).ToList()
        };
        if (scored.Count == 0)
            throw AppException.NotFound(
                "No food candidates satisfy the current constraints.",
                AIErrorCodes.NoCandidates);

        var response = new FoodDiscoveryResponse
        {
            ParsedIntent = ToParsedIntent(intent),
            Results = scored.Take(request.Limit).ToList()
        };

        var log = await TrySaveLogAsync(
            customerId,
            request.NightMarketId,
            AIRecommendationType.FoodDiscovery,
            new
            {
                request.SelectedTagIds,
                request.BudgetMax,
                request.NightMarketId,
                request.SortBy,
                request.Limit,
                request.PreferNearMe,
                HasCoordinates = request.Latitude.HasValue && request.Longitude.HasValue,
                HasFreeText = !string.IsNullOrWhiteSpace(request.Query)
            },
            response.ParsedIntent,
            ToLocationSafeLogResponse(response),
            cancellationToken);

        if (log is not null)
        {
            response.LogId = log.Id;
            log.ResultJson = JsonSerializer.Serialize(ToLocationSafeLogResponse(response), JsonOptions);
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
            Limit = 3,
            SortBy = "BestMatch"
        };

        return await FoodDiscoveryAsync(customerId, request, cancellationToken);
    }

    public async Task<ApiResponse<DiningPlanAssistantResponse>> DiningPlanAssistantAsync(
        Guid? customerId,
        DiningPlanAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        request.PreferredTagIds ??= [];
        request.AvoidTagIds ??= [];
        request.DiningStyle = NormalizeDiningStyle(request.DiningStyle);
        if (request.Budget <= 0)
            throw AppException.BadRequest("Vui lòng chọn ngân sách để AI tạo kế hoạch phù hợp.");

        ValidateDiningPlanRequest(request);
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

        var candidates = await _foodItems.GetAiOrderableCandidatesAsync(
            request.NightMarketId,
            GetVietnamLocalTime(),
            GetCandidateLimit(),
            cancellationToken);
        ApplyEffectivePrices(candidates);
        var scopedCandidates = candidates
            .Where(item => !request.NightMarketId.HasValue || item.Booth.NightMarketId == request.NightMarketId.Value)
            .Where(IsOrderableNow)
            .ToList();
        var options = BuildPlanOptions(scopedCandidates, intent, request)
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
        var resolvedPlanMarketId = request.NightMarketId ?? options.FirstOrDefault()?.NightMarketId;

        response.LogId = Guid.NewGuid();
        await SaveRequiredLogAsync(
            response.LogId,
            customerId,
            resolvedPlanMarketId,
            AIRecommendationType.DiningPlan,
            new StoredDiningPlanRequest(
                resolvedPlanMarketId,
                request.GroupSize,
                request.Budget,
                NormalizeDiningStyle(intent.DiningStyle),
                intent.CurrentPreferredTagIds,
                intent.AvoidTagIds),
            ToParsedIntent(intent),
            ToLocationSafeLogResponse(response),
            cancellationToken);

        return ApiResponse<DiningPlanAssistantResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<DiningPlanReadyResponse>> ConfirmDiningPlanAsync(
        Guid? customerId,
        ConfirmDiningPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var log = await GetOwnedLogAsync(customerId, request.LogId, cancellationToken);
        if (log.RecommendationType != AIRecommendationType.DiningPlan)
            throw AppException.Conflict("The AI log is not a dining plan.", AIErrorCodes.PlanChanged);
        var result = JsonSerializer.Deserialize<DiningPlanAssistantResponse>(log.ResultJson, JsonOptions)
            ?? throw AppException.BadRequest("AI log result is invalid.");
        var option = result.Options.FirstOrDefault(item => item.OptionId == request.OptionId)
            ?? throw AppException.NotFound("Dining plan option was not found.");

        if (option.PlanPreview.Count == 0 || option.PlanPreview.Any(item => item.Quantity < 1))
            throw AppException.Conflict("The dining plan structure has changed.", AIErrorCodes.PlanChanged);

        var candidates = await _foodItems.GetAllFoodItemsByIdsAsync(option.PlanPreview.Select(item => item.FoodItemId).Distinct().ToList());
        ApplyEffectivePrices(candidates);
        var candidateMap = candidates.ToDictionary(item => item.Id);
        foreach (var item in option.PlanPreview)
        {
            if (!candidateMap.TryGetValue(item.FoodItemId, out var current))
                throw AppException.Conflict("A food item in this plan no longer exists.", AIErrorCodes.ItemUnavailable);
            var orderability = CustomerOrderability.Evaluate(current, _timeProvider.GetUtcNow().UtcDateTime);
            if (!orderability.CanOrder)
            {
                var code = orderability.ReasonCode switch
                {
                    CustomerOrderability.MarketUnavailable or CustomerOrderability.MarketClosed => AIErrorCodes.MarketNotOrderable,
                    CustomerOrderability.BoothUnavailable or CustomerOrderability.BoothClosed => AIErrorCodes.BoothNotOrderable,
                    _ => AIErrorCodes.ItemUnavailable
                };
                throw AppException.Conflict(CustomerOrderability.GetPublicMessage(orderability.ReasonCode!), code);
            }
            if (current.Booth.NightMarketId != option.NightMarketId)
                throw AppException.Conflict("The dining plan market has changed.", AIErrorCodes.PlanChanged);
            if (current.Price != item.UnitPrice)
                throw AppException.Conflict("A food item price has changed. Please regenerate the plan.", AIErrorCodes.PriceChanged);
        }

        var currentTotal = option.PlanPreview.Sum(item => candidateMap[item.FoodItemId].Price * item.Quantity);
        if (currentTotal > option.Budget)
            throw AppException.Conflict("The dining plan exceeds its total group budget.", AIErrorCodes.BudgetExceeded);
        if (currentTotal != option.EstimatedTotal)
            throw AppException.Conflict("The dining plan totals have changed.", AIErrorCodes.PlanChanged);

        log.SelectedOptionId = option.OptionId;
        log.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        _logs.Update(log);
        await _logs.SaveChangesAsync();

        var response = new DiningPlanReadyResponse
        {
            NightMarketId = option.NightMarketId,
            NightMarketName = option.NightMarketName,
            GroupSize = option.GroupSize,
            Budget = option.Budget,
            EstimatedTotal = option.EstimatedTotal,
            RemainingBudget = option.RemainingBudget,
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
        if (log.RecommendationType != AIRecommendationType.DiningPlan)
            throw AppException.Conflict("The AI log is not a dining plan.", AIErrorCodes.PlanChanged);
        var original = JsonSerializer.Deserialize<DiningPlanAssistantResponse>(log.ResultJson, JsonOptions)
            ?? throw AppException.BadRequest("AI log result is invalid.");
        var stored = JsonSerializer.Deserialize<StoredDiningPlanRequest>(log.InputJson, JsonOptions)
            ?? throw AppException.Conflict("The original dining-plan request is unavailable.", AIErrorCodes.PlanChanged);
        var rebuiltRequest = new DiningPlanAssistantRequest
        {
            NightMarketId = stored.NightMarketId,
            GroupSize = stored.GroupSize,
            Budget = stored.Budget,
            DiningStyle = NormalizeDiningStyle(stored.DiningStyle),
            PreferredTagIds = stored.CurrentPreferredTagIds,
            AvoidTagIds = stored.AvoidTagIds
        };
        ValidateDiningPlanRequest(rebuiltRequest);

        var tags = await _foodTags.GetActiveAsync(cancellationToken);
        var intent = await BuildIntentAsync(
            null, tags, rebuiltRequest.PreferredTagIds, rebuiltRequest.AvoidTagIds,
            customerId, rebuiltRequest.Budget, rebuiltRequest.DiningStyle, cancellationToken);
        var candidates = await _foodItems.GetAiOrderableCandidatesAsync(
            rebuiltRequest.NightMarketId, GetVietnamLocalTime(), GetCandidateLimit(), cancellationToken);
        ApplyEffectivePrices(candidates);
        var previousSignatures = original.Options.Select(option => GetPlanSignature(option.PlanPreview)).ToHashSet(StringComparer.Ordinal);
        var options = BuildPlanOptions(
            candidates.Where(IsOrderableNow).ToList(), intent, rebuiltRequest,
            previousSignatures, request.Priority).Take(3).ToList();

        var regenerated = new DiningPlanAssistantResponse
        {
            LogId = Guid.NewGuid(),
            Step = options.Count == 0 ? "NO_PLAN_FOUND" : "PLAN_OPTIONS",
            Message = options.Count == 0
                ? "Không còn phương án có thể đặt ngay trong ngân sách hiện tại."
                : "Đã tạo lại phương án từ dữ liệu và trạng thái hiện tại.",
            Options = options
        };
        await SaveRequiredLogAsync(
            regenerated.LogId, customerId, rebuiltRequest.NightMarketId, AIRecommendationType.DiningPlan,
            stored, ToParsedIntent(intent), ToLocationSafeLogResponse(regenerated), cancellationToken);
        return ApiResponse<DiningPlanAssistantResponse>.SuccessResponse(regenerated);
    }

    private async Task<List<DiningPlanOptionResponse>> RefreshPlanOptionsAsync(
        IReadOnlyCollection<DiningPlanOptionResponse> options,
        CancellationToken cancellationToken)
    {
        var refreshed = new List<DiningPlanOptionResponse>();
        foreach (var option in options)
        {
            var candidates = await _foodItems.GetAiCandidatesAsync(
                option.NightMarketId,
                GetCandidateLimit(),
                cancellationToken);
            ApplyEffectivePrices(candidates);
            var candidateMap = candidates.ToDictionary(item => item.Id);
            if (option.PlanPreview.Any(item => !candidateMap.ContainsKey(item.FoodItemId)))
                continue;

            var items = option.PlanPreview.Select(item =>
            {
                var current = candidateMap[item.FoodItemId];
                return new DiningPlanItemResponse
                {
                    FoodItemId = current.Id,
                    FoodName = current.Name,
                    BoothId = current.BoothId,
                    BoothName = current.Booth.BoothName,
                    BoothSlotCode = current.Booth.SlotNumber ?? current.Booth.BoothCode,
                    ZoneName = current.Booth.Zone?.ZoneName,
                    Quantity = item.Quantity,
                    UnitPrice = current.Price,
                    TotalPrice = current.Price * item.Quantity,
                    Role = item.Role
                };
            }).ToList();

            RepairPlanToBudget(items, option.Budget);
            var total = items.Sum(item => item.TotalPrice);
            if (items.Count == 0 || total > option.Budget)
                continue;

            option.PlanPreview = items;
            option.EstimatedTotal = total;
            option.RemainingBudget = option.Budget - total;
            option.FeasibilityStatus = "WithinBudget";
            option.DistanceMeters = null;
            option.Reason = $"Có món thật đang bán tại {option.NightMarketName}, tổng dự kiến {total:N0} trong ngân sách {option.Budget:N0}.";
            refreshed.Add(option);
        }

        return refreshed;
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
            HasReason = !string.IsNullOrWhiteSpace(request.Reason)
        };

        var feedbackLogId = Guid.NewGuid();
        await SaveRequiredLogAsync(
            feedbackLogId, customerId, null, AIRecommendationType.PreferenceProfile,
            feedback, null, new { Status = "Recorded", request.FeedbackType }, cancellationToken);

        return ApiResponse<AIFeedbackResponse>.SuccessResponse(new AIFeedbackResponse
        {
            FeedbackLogId = feedbackLogId,
            Message = "Cảm ơn bạn, phản hồi đã được ghi nhận."
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            providerIntent = new FoodIntentDto();
        }

        var allowedTagIds = allowedTags.Select(tag => tag.Id).ToHashSet();
        var requestedIds = preferredTagIds.Concat(avoidTagIds).Distinct().ToList();
        if (requestedIds.Any(id => !allowedTagIds.Contains(id)))
            throw AppException.BadRequest("One or more preference tags are invalid.");

        var currentPreferred = preferredTagIds.ToHashSet();
        var savedPreferred = new HashSet<Guid>();
        var avoid = avoidTagIds.ToHashSet();
        foreach (var tagName in providerIntent.MatchedTagNames ?? [])
        {
            var tag = FindTag(allowedTags, tagName);
            if (tag is not null) currentPreferred.Add(tag.Id);
        }
        foreach (var tagName in providerIntent.AvoidTagNames ?? [])
        {
            var tag = FindTag(allowedTags, tagName);
            if (tag is not null) avoid.Add(tag.Id);
        }

        AddDiningStyleTags(currentPreferred, allowedTags, diningStyle ?? providerIntent.DiningStyle);

        var history = CustomerRecommendationContext.Empty;
        if (customerId.HasValue)
        {
            var preferences = await _preferences.GetByCustomerAsync(customerId.Value, cancellationToken);
            foreach (var preference in preferences)
            {
                if (!allowedTagIds.Contains(preference.FoodTagId))
                    continue;
                if (preference.PreferenceKind == CustomerPreferenceKind.Like)
                    savedPreferred.Add(preference.FoodTagId);
                if (preference.PreferenceKind == CustomerPreferenceKind.Avoid)
                    avoid.Add(preference.FoodTagId);
            }

            try
            {
                history = await _customerContext.GetAsync(
                    customerId.Value,
                    Math.Clamp(_settings.HistoryOrderLimit, 1, 50),
                    Math.Clamp(_settings.HistoryReviewLimit, 1, 50),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                history = CustomerRecommendationContext.Empty;
            }
        }

        foreach (var tagId in avoid)
        {
            currentPreferred.Remove(tagId);
            savedPreferred.Remove(tagId);
        }

        var preferred = currentPreferred
            .Concat(savedPreferred)
            .Concat(history.TagQuantities.Keys.Where(tagId => !avoid.Contains(tagId) && allowedTagIds.Contains(tagId)))
            .ToHashSet();

        return new ResolvedIntent(
            preferred,
            avoid,
            budget ?? NormalizeProviderBudget(providerIntent.BudgetMax),
            diningStyle is null ? NormalizeProviderDiningStyle(providerIntent.DiningStyle) : NormalizeDiningStyle(diningStyle),
            allowedTags.ToDictionary(tag => tag.Id),
            currentPreferred,
            savedPreferred,
            history);
    }

    private static void AddDiningStyleTags(
        ISet<Guid> currentPreferred,
        IReadOnlyCollection<FoodTag> allowedTags,
        string? diningStyle)
    {
        var codes = diningStyle?.Trim().ToLowerInvariant() switch
        {
            "fullmeal" or "filling" => new[] { "FULLMEAL" },
            "lightmeal" or "light" => new[] { "MILD", "SNACK" },
            "foodtour" => new[] { "SHAREABLE", "SNACK" },
            "datenight" => new[] { "DESSERT", "DRINK" },
            "family" or "sharing" => new[] { "SHAREABLE", "FULLMEAL" },
            "snack" => new[] { "SNACK" },
            "drink" => new[] { "DRINK" },
            _ => Array.Empty<string>()
        };

        foreach (var code in codes)
        {
            var tag = allowedTags.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (tag is not null) currentPreferred.Add(tag.Id);
        }
    }

    private FoodDiscoveryItemResponse ToDiscoveryItem(
        FoodItem item,
        ResolvedIntent intent,
        IReadOnlyDictionary<Guid, double?> marketDistances)
    {
        var itemTagIds = item.FoodItemTags.Select(tag => tag.FoodTagId).ToHashSet();
        if (intent.AvoidTagIds.Overlaps(itemTagIds))
        {
            return new FoodDiscoveryItemResponse { MatchScore = 0 };
        }

        var distanceMeters = marketDistances.GetValueOrDefault(item.Booth.NightMarketId);
        var score = ScoreItem(item, intent, distanceMeters);

        var matchedTags = item.FoodItemTags
            .Where(tag => intent.PreferredTagIds.Contains(tag.FoodTagId))
            .Select(tag => tag.FoodTag.Name)
            .ToList();
        var matchedCurrentTags = item.FoodItemTags
            .Where(tag => intent.CurrentPreferredTagIds.Contains(tag.FoodTagId))
            .Select(tag => tag.FoodTag.Name)
            .ToList();
        var matchedSavedTags = item.FoodItemTags
            .Where(tag => intent.SavedPreferredTagIds.Contains(tag.FoodTagId))
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
            BoothRating = item.Booth.AverageRating ?? 0,
            CanOrder = IsOrderableNow(item),
            DistanceMeters = distanceMeters,
            MatchScore = (int)Math.Round(Math.Clamp(score, 0, 100)),
            MatchedPreferences = matchedTags,
            MatchedCurrentPreferences = matchedCurrentTags,
            MatchedSavedPreferences = matchedSavedTags,
            IsFallback = intent.CurrentPreferredTagIds.Count > 0 && !intent.CurrentPreferredTagIds.Overlaps(itemTagIds),
            Reason = BuildReason(item.Name, matchedTags, item.Price, intent.BudgetMax, item.Booth.AverageRating, item.Booth.NightMarket.Name, distanceMeters),
            Tags = item.FoodItemTags
                .Where(tag => tag.FoodTag.Status == FoodTagStatus.Active)
                .Select(tag => tag.FoodTag.Code)
                .ToList()
        };
    }

    private IReadOnlyCollection<DiningPlanOptionResponse> BuildPlanOptions(
        IReadOnlyCollection<FoodItem> candidates,
        ResolvedIntent intent,
        DiningPlanAssistantRequest request,
        IReadOnlySet<string>? excludedCombinations = null,
        string? preferredStrategy = null)
    {
        var marketGroup = candidates
            .GroupBy(item => item.Booth.NightMarketId)
            .OrderByDescending(group => group.Max(item => ScoreItem(item, intent)))
            .ThenByDescending(group => group.Max(item => item.Booth.AverageRating ?? 0))
            .ThenBy(group => group.Key)
            .FirstOrDefault();
        if (marketGroup is null) return [];

        var strategies = new[] { "BestMatch", "BudgetFriendly", "HighRating" };
        if (!string.IsNullOrWhiteSpace(preferredStrategy))
        {
            var normalized = NormalizeStrategy(preferredStrategy);
            strategies = strategies.OrderBy(strategy => strategy == normalized ? 0 : 1).ToArray();
        }

        var options = new List<DiningPlanOptionResponse>();
        var combinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var strategy in strategies)
        {
            var option = BuildPlanOption(marketGroup.ToList(), intent, request, strategy);
            if (option is null) continue;
            var signature = GetPlanSignature(option.PlanPreview);
            if (!combinations.Add(signature)) continue;
            if (excludedCombinations?.Contains(signature) == true) continue;
            options.Add(option);
        }

        if (options.Count == 0 && excludedCombinations is not null)
            return BuildPlanOptions(candidates, intent, request, null, preferredStrategy);
        return options;
    }

    private DiningPlanOptionResponse? BuildPlanOption(
        IReadOnlyCollection<FoodItem> marketItems,
        ResolvedIntent intent,
        DiningPlanAssistantRequest request,
        string strategy)
    {
        var distanceMeters = CalculateDistanceMeters(
            request.PreferNearMe,
            request.NightMarketId.HasValue,
            request.Latitude,
            request.Longitude,
            marketItems.First().Booth.NightMarket);
        var eligible = marketItems
            .Where(IsOrderableNow)
            .Where(item => !intent.AvoidTagIds.Overlaps(item.FoodItemTags.Select(tag => tag.FoodTagId).ToHashSet()));
        var filtered = strategy switch
        {
            "BudgetFriendly" => eligible.OrderBy(item => item.Price)
                .ThenByDescending(item => ScoreItem(item, intent, distanceMeters)).ThenBy(item => item.Id).ToList(),
            "HighRating" => eligible.OrderByDescending(item => item.Booth.AverageRating ?? 0)
                .ThenByDescending(item => ScoreItem(item, intent, distanceMeters)).ThenBy(item => item.Price).ThenBy(item => item.Id).ToList(),
            _ => eligible.OrderByDescending(item => ScoreItem(item, intent, distanceMeters))
                .ThenByDescending(item => item.Booth.AverageRating ?? 0).ThenBy(item => item.Price).ThenBy(item => item.Id).ToList()
        };

        if (filtered.Count == 0) return null;

        var planItems = new List<DiningPlanItemResponse>();
        AddDiningStyleRoles(planItems, filtered, request);

        if (planItems.Count == 0)
        {
            var first = filtered.First();
            planItems.Add(ToPlanItem(first, request.GroupSize, "MainDish"));
        }

        RepairPlanToBudget(planItems, request.Budget);
        var total = planItems.Sum(item => item.TotalPrice);
        if (planItems.Count == 0 || total > request.Budget) return null;

        var market = filtered.First().Booth.NightMarket;
        var score = (int)Math.Round(planItems.Average(item =>
            ScoreItem(filtered.First(food => food.Id == item.FoodItemId), intent, distanceMeters)));

        return new DiningPlanOptionResponse
        {
            OptionId = strategy switch
            {
                "BudgetFriendly" => "OPT_BUDGET_FRIENDLY",
                "HighRating" => "OPT_HIGH_RATING",
                _ => "OPT_BEST_MATCH"
            },
            OptionType = strategy,
            FeasibilityStatus = "OrderableNow",
            Label = strategy switch
            {
                "BudgetFriendly" => "Tiết kiệm hơn",
                "HighRating" => "Ưu tiên rating gian hàng",
                _ => "Phù hợp nhất"
            },
            NightMarketId = market.Id,
            NightMarketName = market.Name,
            GroupSize = request.GroupSize,
            MatchScore = Math.Clamp(score, 0, 100),
            EstimatedTotal = total,
            Budget = request.Budget,
            RemainingBudget = request.Budget - total,
            DistanceMeters = distanceMeters,
            PlanPreview = planItems,
            Reason = distanceMeters.HasValue
                ? $"Có món thật tại {market.Name}, cách vị trí của bạn khoảng {distanceMeters.Value:N0} m; tổng dự kiến {total:N0} trong ngân sách {request.Budget:N0}."
                : $"Các món hiện có thể đặt tại {market.Name}; tổng dự kiến {total:N0} trong ngân sách nhóm {request.Budget:N0}."
        };
    }

    private static void AddDiningStyleRoles(
        ICollection<DiningPlanItemResponse> planItems,
        IReadOnlyCollection<FoodItem> candidates,
        DiningPlanAssistantRequest request)
    {
        var sharedQuantity = Math.Max(1, (int)Math.Ceiling(request.GroupSize / 2d));
        switch (request.DiningStyle.Trim().ToLowerInvariant())
        {
            case "lightmeal":
            case "light":
                TryAddRole(planItems, candidates, "LightMeal", request.GroupSize, ["MILD", "SOUP", "SNACK"]);
                TryAddRole(planItems, candidates, "Drink", request.GroupSize, ["DRINK", "COLD"]);
                break;
            case "foodtour":
                TryAddRole(planItems, candidates, "Shareable", sharedQuantity, ["SHAREABLE", "GRILLED", "FRIED"]);
                TryAddRole(planItems, candidates, "Snack", sharedQuantity, ["SNACK"]);
                TryAddRole(planItems, candidates, "Drink", request.GroupSize, ["DRINK", "COLD"]);
                break;
            case "datenight":
                TryAddRole(planItems, candidates, "Shareable", sharedQuantity, ["SHAREABLE", "FULLMEAL"]);
                TryAddRole(planItems, candidates, "Dessert", sharedQuantity, ["DESSERT", "SWEET"]);
                TryAddRole(planItems, candidates, "Drink", request.GroupSize, ["DRINK", "COLD"]);
                break;
            case "family":
            case "sharing":
                TryAddRole(planItems, candidates, "MainDish", request.GroupSize, ["FULLMEAL", "RICE", "NOODLE"]);
                TryAddRole(planItems, candidates, "Shareable", sharedQuantity, ["SHAREABLE", "GRILLED", "FRIED"]);
                TryAddRole(planItems, candidates, "Drink", request.GroupSize, ["DRINK", "COLD"]);
                break;
            default:
                TryAddRole(planItems, candidates, "MainDish", request.GroupSize, ["FULLMEAL", "RICE", "NOODLE", "BEEF", "CHICKEN", "PORK"]);
                TryAddRole(planItems, candidates, "Drink", request.GroupSize, ["DRINK", "COLD"]);
                TryAddRole(planItems, candidates, "Snack", sharedQuantity, ["SNACK", "SHAREABLE", "FRIED", "GRILLED"]);
                break;
        }
    }

    private static void RepairPlanToBudget(List<DiningPlanItemResponse> items, decimal budget)
    {
        while (items.Count > 0 && items.Sum(item => item.TotalPrice) > budget)
        {
            var reducible = items
                .Where(item => item.Quantity > 1)
                .OrderByDescending(item => item.UnitPrice)
                .FirstOrDefault();
            if (reducible is not null)
            {
                reducible.Quantity--;
                reducible.TotalPrice = reducible.UnitPrice * reducible.Quantity;
                continue;
            }

            var removable = items
                .OrderBy(item => item.Role == "MainDish" ? 1 : 0)
                .ThenByDescending(item => item.TotalPrice)
                .First();
            items.Remove(removable);
        }
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
            && food.FoodItemTags.Any(tag =>
                tag.FoodTag.Status == FoodTagStatus.Active
                && tagCodes.Contains(tag.FoodTag.Code)));
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

    private static double ScoreItem(FoodItem item, ResolvedIntent intent, double? distanceMeters = null)
    {
        var tagIds = item.FoodItemTags.Select(tag => tag.FoodTagId).ToHashSet();
        if (intent.AvoidTagIds.Overlaps(tagIds)) return 0;

        var score = 15d;
        score += MatchRatio(intent.CurrentPreferredTagIds, tagIds) * 45;
        score += MatchRatio(intent.SavedPreferredTagIds, tagIds) * 20;

        if (intent.History.TagQuantities.Count > 0)
        {
            var max = intent.History.TagQuantities.Values.Max();
            var affinity = tagIds
                .Where(intent.History.TagQuantities.ContainsKey)
                .Select(id => intent.History.TagQuantities[id])
                .DefaultIfEmpty(0)
                .Max();
            score += affinity * 8d / max;
        }
        if (intent.History.CategoryQuantities.Count > 0
            && intent.History.CategoryQuantities.TryGetValue(item.CategoryId, out var categoryQuantity))
            score += categoryQuantity * 6d / intent.History.CategoryQuantities.Values.Max();

        if (intent.History.NegativeBoothIds.Contains(item.BoothId))
            score -= 8;
        else
        {
            if (intent.History.PositiveBoothIds.Contains(item.BoothId)) score += 5;
            if (intent.History.BoothQuantities.TryGetValue(item.BoothId, out var boothQuantity))
                score += boothQuantity * 3d / intent.History.BoothQuantities.Values.Max();
        }

        if (intent.History.HasHistory && !intent.History.RecentFoodIds.Contains(item.Id)) score += 3;
        if (intent.History.FoodFeedbackScores?.TryGetValue(item.Id, out var feedbackScore) == true)
            score += feedbackScore * 2;
        if (!intent.BudgetMax.HasValue && intent.History.TypicalUnitPrice is > 0)
        {
            var deviation = Math.Abs(item.Price - intent.History.TypicalUnitPrice.Value) / intent.History.TypicalUnitPrice.Value;
            if (deviation <= 0.25m) score += 2;
        }
        if (intent.BudgetMax.HasValue && item.Price <= intent.BudgetMax.Value) score += 5;
        if (item.IsFeatured) score += 2;
        score += Math.Min(8, (double)(item.Booth.AverageRating ?? 0) * 1.6);
        score += DistanceBoost(distanceMeters);
        return Math.Clamp(score, 0, 100);
    }

    private static double MatchRatio(IReadOnlySet<Guid> preferences, IReadOnlySet<Guid> itemTags)
        => preferences.Count == 0 ? 0 : preferences.Count(itemTags.Contains) / (double)preferences.Count;

    private static double DistanceBoost(double? distanceMeters)
        => distanceMeters switch
        {
            <= 1_000 => 8,
            <= 3_000 => 6,
            <= 10_000 => 4,
            <= 25_000 => 2,
            _ => 0
        };

    private async Task<AIRecommendationLog> SaveLogAsync(
        Guid? customerId,
        Guid? nightMarketId,
        AIRecommendationType type,
        object input,
        object? parsedIntent,
        object result,
        CancellationToken cancellationToken,
        Guid? logId = null)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var log = new AIRecommendationLog
        {
            Id = logId ?? Guid.NewGuid(),
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

    private async Task<AIRecommendationLog> SaveRequiredLogAsync(
        Guid logId,
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
            return await SaveLogAsync(
                customerId, nightMarketId, type, input, parsedIntent, result, cancellationToken, logId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw AppException.ServiceUnavailable(
                "The AI result could not be persisted. Please try again.",
                AIErrorCodes.PlanLogUnavailable,
                exception);
        }
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
        if (!customerId.HasValue)
            throw AppException.Unauthorized("A customer identity is required.");
        if (log.CustomerId != customerId)
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
            MatchedTags = intent.CurrentPreferredTagIds
                .Concat(intent.SavedPreferredTagIds)
                .Distinct()
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

    private static FoodDiscoveryResponse ToLocationSafeLogResponse(FoodDiscoveryResponse response)
    {
        var copy = JsonSerializer.Deserialize<FoodDiscoveryResponse>(JsonSerializer.Serialize(response, JsonOptions), JsonOptions)!;
        foreach (var item in copy.Results)
        {
            if (item.DistanceMeters.HasValue)
            {
                item.DistanceMeters = null;
                item.Reason = $"{item.FoodName} là món hiển thị hợp lệ tại {item.NightMarketName}, giá {item.Price:N0}.";
            }
        }
        return copy;
    }

    private static DiningPlanAssistantResponse ToLocationSafeLogResponse(DiningPlanAssistantResponse response)
    {
        var copy = JsonSerializer.Deserialize<DiningPlanAssistantResponse>(JsonSerializer.Serialize(response, JsonOptions), JsonOptions)!;
        foreach (var option in copy.Options)
        {
            if (option.DistanceMeters.HasValue)
            {
                option.DistanceMeters = null;
                option.Reason = $"Phương án tại {option.NightMarketName}, tổng dự kiến {option.EstimatedTotal:N0} trong ngân sách {option.Budget:N0}.";
            }
        }
        return copy;
    }

    private void ValidateDiscoveryRequest(FoodDiscoveryRequest request)
    {
        ValidateQueryAndTags(request.Query, request.SelectedTagIds);
        ValidateCoordinates(request.PreferNearMe, request.Latitude, request.Longitude);
        if (request.BudgetMax is <= 0 || request.BudgetMax > _settings.MaxBudget)
            throw AppException.BadRequest("BudgetMax is outside the supported range.");
    }

    private void ValidateDiningPlanRequest(DiningPlanAssistantRequest request)
    {
        ValidateQueryAndTags(request.Query, request.PreferredTagIds.Concat(request.AvoidTagIds).ToList());
        ValidateCoordinates(request.PreferNearMe, request.Latitude, request.Longitude);
        if (request.GroupSize is < 1 or > 50)
            throw AppException.BadRequest("GroupSize must be between 1 and 50.", AIErrorCodes.InvalidRequest);
        if (request.Budget <= 0 || request.Budget > _settings.MaxBudget)
            throw AppException.BadRequest("Budget is outside the supported range.", AIErrorCodes.InvalidRequest);
        if (request.DiningStyle.Length > 50)
            throw AppException.BadRequest("DiningStyle is too long.");
        if (request.PreferredTagIds.Intersect(request.AvoidTagIds).Any())
            throw AppException.BadRequest("Preferred and avoid tags must not overlap.", AIErrorCodes.InvalidRequest);
    }

    private void ValidateQueryAndTags(string? query, IReadOnlyCollection<Guid> tagIds)
    {
        if (query?.Length > Math.Clamp(_settings.MaxQueryLength, 1, 1000))
            throw AppException.BadRequest("The preference query is too long.");
        if (tagIds.Distinct().Count() > Math.Clamp(_settings.MaxPreferenceTags, 1, 50))
            throw AppException.BadRequest("Too many preference tags were supplied.");
    }

    private int GetCandidateLimit() => Math.Clamp(_settings.CandidateLimit, 1, 500);

    private static void ValidateCoordinates(bool preferNearMe, decimal? latitude, decimal? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
            throw AppException.BadRequest("Latitude and Longitude must be supplied together.", "AI_LOCATION_INCOMPLETE");
        if (preferNearMe && (!latitude.HasValue || !longitude.HasValue))
            throw AppException.BadRequest("PreferNearMe requires Latitude and Longitude.", "AI_LOCATION_REQUIRED");
        if (latitude is < -90 or > 90)
            throw AppException.BadRequest("Latitude must be between -90 and 90.", "AI_LATITUDE_INVALID");
        if (longitude is < -180 or > 180)
            throw AppException.BadRequest("Longitude must be between -180 and 180.", "AI_LONGITUDE_INVALID");
    }

    private static double? CalculateDistanceMeters(
        bool preferNearMe,
        bool marketSelected,
        decimal? latitude,
        decimal? longitude,
        NightMarket market)
    {
        if (!preferNearMe || marketSelected || !latitude.HasValue || !longitude.HasValue
            || !market.Latitude.HasValue || !market.Longitude.HasValue)
            return null;

        const double earthRadiusMeters = 6_371_000;
        static double Radians(double degrees) => degrees * Math.PI / 180d;

        var fromLatitude = Radians((double)latitude.Value);
        var toLatitude = Radians((double)market.Latitude.Value);
        var latitudeDelta = toLatitude - fromLatitude;
        var longitudeDelta = Radians((double)(market.Longitude.Value - longitude.Value));
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(fromLatitude) * Math.Cos(toLatitude) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        var distance = 2 * earthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
        return Math.Round(distance);
    }

    private decimal? NormalizeProviderBudget(decimal? providerBudget)
        => providerBudget is > 0 && providerBudget <= _settings.MaxBudget ? providerBudget : null;

    private bool IsOrderableNow(FoodItem item)
        => CustomerOrderability.Evaluate(item, _timeProvider.GetUtcNow().UtcDateTime).CanOrder;

    private TimeOnly GetVietnamLocalTime()
        => TimeOnly.FromDateTime(NightMarketAvailability.GetVietnamLocalTime(_timeProvider.GetUtcNow().UtcDateTime));

    private static string GetPlanSignature(IReadOnlyCollection<DiningPlanItemResponse> items)
        => string.Join('|', items.OrderBy(item => item.FoodItemId)
            .Select(item => $"{item.FoodItemId:N}:{item.Quantity}"));

    private static string NormalizeStrategy(string? strategy)
        => strategy?.Trim().ToLowerInvariant() switch
        {
            "cheaper" or "budgetfriendly" or "budget_friendly" => "BudgetFriendly",
            "higherrated" or "highrating" or "high_rating" => "HighRating",
            _ => "BestMatch"
        };

    private static string NormalizeDiningStyle(string? diningStyle)
        => diningStyle?.Trim().ToLowerInvariant() switch
        {
            "lightmeal" or "light" => "LightMeal",
            "foodtour" => "FoodTour",
            "datenight" => "DateNight",
            "family" or "sharing" => "Family",
            "snack" => "Snack",
            "drink" => "Drink",
            _ => "FullMeal"
        };

    private static string? NormalizeProviderDiningStyle(string? diningStyle)
        => string.IsNullOrWhiteSpace(diningStyle) ? null : NormalizeDiningStyle(diningStyle);

    private static string BuildReason(
        string foodName,
        IReadOnlyCollection<string> matchedTags,
        decimal price,
        decimal? budget,
        decimal? rating,
        string marketName,
        double? distanceMeters)
    {
        var tagText = matchedTags.Count == 0 ? "một phương án thay thế đang hiển thị" : string.Join(", ", matchedTags);
        var budgetText = budget.HasValue ? $", nằm trong ngân sách {budget.Value:N0}" : string.Empty;
        var ratingText = rating.HasValue ? $", điểm gian hàng {rating.Value:0.0} sao" : string.Empty;
        var distanceText = distanceMeters.HasValue ? $", cách vị trí của bạn khoảng {distanceMeters.Value:N0} m" : string.Empty;
        return $"{foodName} phù hợp với {tagText}, giá {price:N0}{budgetText}{ratingText}, tại {marketName}{distanceText}.";
    }

    private void ApplyEffectivePrices(IReadOnlyCollection<FoodItem> items)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var item in items)
            item.Price = FoodPriceResolver.GetCurrentPrice(item, utcNow);
    }

    private sealed record ResolvedIntent(
        HashSet<Guid> PreferredTagIds,
        HashSet<Guid> AvoidTagIds,
        decimal? BudgetMax,
        string? DiningStyle,
        IReadOnlyDictionary<Guid, FoodTag> TagMap,
        HashSet<Guid> CurrentPreferredTagIds,
        HashSet<Guid> SavedPreferredTagIds,
        CustomerRecommendationContext History);

    private sealed record StoredDiningPlanRequest(
        Guid? NightMarketId,
        int GroupSize,
        decimal Budget,
        string DiningStyle,
        IReadOnlyCollection<Guid> CurrentPreferredTagIds,
        IReadOnlyCollection<Guid> AvoidTagIds);
}
