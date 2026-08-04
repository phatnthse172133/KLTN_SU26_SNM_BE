using DomainLayer.Common;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public sealed class AICustomerContextRepository : IAICustomerContextRepository
{
    private readonly SNMDbContext _context;

    public AICustomerContextRepository(SNMDbContext context) => _context = context;

    public async Task<CustomerRecommendationContext> GetAsync(
        Guid customerId,
        int maxCompletedOrders,
        int maxReviews,
        CancellationToken cancellationToken = default)
    {
        maxCompletedOrders = Math.Clamp(maxCompletedOrders, 1, 50);
        maxReviews = Math.Clamp(maxReviews, 1, 50);

        var orderIds = await _context.Orders
            .AsNoTracking()
            .Where(order =>
                order.CustomerId == customerId &&
                order.Status == OrderStatus.Completed &&
                order.Payments.Any(payment => payment.Status == PaymentStatus.Paid))
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Select(order => order.Id)
            .Take(maxCompletedOrders)
            .ToListAsync(cancellationToken);

        if (orderIds.Count == 0)
            return await WithFeedbackAndReviewSignalsAsync(customerId, maxReviews, CustomerRecommendationContext.Empty, cancellationToken);

        var details = _context.OrderDetails
            .AsNoTracking()
            .Where(detail => orderIds.Contains(detail.OrderId));

        var categoryQuantities = await details
            .GroupBy(detail => detail.FoodItem.CategoryId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(detail => detail.Quantity) })
            .ToDictionaryAsync(row => row.Id, row => row.Quantity, cancellationToken);

        var tagQuantities = await details
            .SelectMany(detail => detail.FoodItem.FoodItemTags.Select(tag => new
            {
                tag.FoodTagId,
                detail.Quantity
            }))
            .GroupBy(row => row.FoodTagId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(row => row.Quantity) })
            .ToDictionaryAsync(row => row.Id, row => row.Quantity, cancellationToken);

        var boothQuantities = await details
            .GroupBy(detail => detail.FoodItem.BoothId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(detail => detail.Quantity) })
            .ToDictionaryAsync(row => row.Id, row => row.Quantity, cancellationToken);

        var recentFoodIds = (await details
                .OrderByDescending(detail => detail.Order.CreatedAt)
                .ThenByDescending(detail => detail.CreatedAt)
                .Select(detail => detail.FoodItemId)
                .Take(maxCompletedOrders * 5)
                .ToListAsync(cancellationToken))
            .Distinct()
            .ToHashSet();

        var typicalUnitPrice = await details
            .Select(detail => (decimal?)detail.UnitPrice)
            .AverageAsync(cancellationToken);

        var context = new CustomerRecommendationContext(
            tagQuantities,
            categoryQuantities,
            boothQuantities,
            recentFoodIds,
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            typicalUnitPrice);

        return await WithFeedbackAndReviewSignalsAsync(customerId, maxReviews, context, cancellationToken);
    }

    private async Task<CustomerRecommendationContext> WithFeedbackAndReviewSignalsAsync(
        Guid customerId,
        int maxReviews,
        CustomerRecommendationContext context,
        CancellationToken cancellationToken)
    {
        var reviews = await _context.Reviews
            .AsNoTracking()
            .Where(review =>
                review.CustomerId == customerId &&
                review.Order.Status == OrderStatus.Completed &&
                review.Order.Payments.Any(payment => payment.Status == PaymentStatus.Paid))
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .Select(review => new { review.BoothId, review.Rating })
            .Take(maxReviews)
            .ToListAsync(cancellationToken);

        var positive = reviews.Where(review => review.Rating >= 4).Select(review => review.BoothId).ToHashSet();
        var negative = reviews.Where(review => review.Rating <= 2).Select(review => review.BoothId).ToHashSet();
        positive.ExceptWith(negative);

        var feedbackRows = await _context.AIRecommendationLogs
            .AsNoTracking()
            .Where(log => log.CustomerId == customerId && log.RecommendationType == AIRecommendationType.PreferenceProfile)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Select(log => log.InputJson)
            .Take(50)
            .ToListAsync(cancellationToken);

        var parsed = feedbackRows.Select(ParseFeedback).Where(item => item is not null).Select(item => item!).ToList();
        var sourceIds = parsed.Where(item => item.SourceLogId.HasValue).Select(item => item.SourceLogId!.Value).Distinct().ToList();
        var sources = sourceIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.AIRecommendationLogs.AsNoTracking()
                .Where(log => log.CustomerId == customerId && sourceIds.Contains(log.Id))
                .ToDictionaryAsync(log => log.Id, log => log.ResultJson, cancellationToken);
        var scores = new Dictionary<Guid, int>();
        foreach (var item in parsed)
        {
            var foodIds = item.FoodItemId.HasValue
                ? new[] { item.FoodItemId.Value }
                : ResolveOptionFoodIds(item, sources);
            foreach (var foodId in foodIds)
                scores[foodId] = Math.Clamp(scores.GetValueOrDefault(foodId) + item.Weight, -3, 3);
        }

        return context with
        {
            PositiveBoothIds = positive,
            NegativeBoothIds = negative,
            FoodFeedbackScores = scores
        };
    }

    private static ParsedFeedback? ParseFeedback(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.TryGetProperty("feedbackType", out var typeElement) ? typeElement.GetString() : null;
            var weight = type?.Trim().ToLowerInvariant() switch
            {
                "like" or "liked" or "suitable" or "helpful" or "relevant" or "positive" => 1,
                "dislike" or "notsuitable" or "not_suitable" or "irrelevant" or "negative" => -1,
                _ => 0
            };
            if (weight == 0) return null;
            return new ParsedFeedback(
                ReadGuid(root, "foodItemId"),
                ReadGuid(root, "logId"),
                root.TryGetProperty("optionId", out var option) ? option.GetString() : null,
                weight);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyCollection<Guid> ResolveOptionFoodIds(
        ParsedFeedback feedback,
        IReadOnlyDictionary<Guid, string> sourceLogs)
    {
        if (!feedback.SourceLogId.HasValue || string.IsNullOrWhiteSpace(feedback.OptionId)
            || !sourceLogs.TryGetValue(feedback.SourceLogId.Value, out var json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("options", out var options)) return [];
            foreach (var option in options.EnumerateArray())
            {
                if (!option.TryGetProperty("optionId", out var id) || id.GetString() != feedback.OptionId) continue;
                if (!option.TryGetProperty("planPreview", out var preview)) return [];
                return preview.EnumerateArray()
                    .Select(item => ReadGuid(item, "foodItemId"))
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();
            }
        }
        catch (JsonException)
        {
        }
        return [];
    }

    private static Guid? ReadGuid(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value)
            && Guid.TryParse(value.GetString(), out var id) ? id : null;

    private sealed record ParsedFeedback(Guid? FoodItemId, Guid? SourceLogId, string? OptionId, int Weight);
}
