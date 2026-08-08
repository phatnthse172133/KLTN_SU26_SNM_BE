using DomainLayer.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IBoothDashboardRepository
{
    Task<List<OrderRevenueRow>> GetCompletedOrdersAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<List<OrderStatusCountRow>> GetOrderStatusCountsAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<List<TopFoodRow>> GetTopFoodsAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, int top, CancellationToken ct = default);
    Task<List<BoothPeakHourRow>> GetPeakHoursAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<(double avgRating, int count)> GetRatingSummaryAsync(Guid boothId, CancellationToken ct = default);
    Task<int> GetOutOfStockCountAsync(Guid boothId, CancellationToken ct = default);
    Task<List<ActivePromotionRow>> GetActivePromotionsAsync(Guid boothId, DateTime nowUtc, CancellationToken ct = default);
    Task<List<RatingTrendRow>> GetRatingTrendAsync(Guid boothId, DateTime fromDate, DateTime toDate, string granularity, CancellationToken ct = default);
    Task<ConversionRow> GetConversionAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<int> GetDistinctCustomerCountAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<int> GetActiveMenuItemCountAsync(Guid boothId, CancellationToken ct = default);
    Task<List<RecentOrderRow>> GetRecentOrdersAsync(Guid boothOwnerId, int count, CancellationToken ct = default);
    Task<List<TopFoodRow>> GetTopFoodsByRevenueAsync(Guid boothOwnerId, DateTime fromDate, DateTime toDate, int top, CancellationToken ct = default);
    Task<List<PromotionPerformanceRow>> GetPromotionPerformanceAsync(Guid boothId, CancellationToken ct = default);
    Task<int> GetFeaturedFoodCountAsync(Guid boothId, CancellationToken ct = default);
}

public class OrderRevenueRow
{
    public Guid OrderId { get; set; }
    public decimal FinalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasPaidPayment { get; set; }
}

public class OrderStatusCountRow
{
    public OrderStatus Status { get; set; }
    public int Count { get; set; }
}

public class TopFoodRow
{
    public Guid FoodItemId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class BoothPeakHourRow
{
    public int Hour { get; set; }
    public int OrderCount { get; set; }
}

public class ActivePromotionRow
{
    public Guid PromotionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public PromotionStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class RatingTrendRow
{
    public DateTime BucketStart { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class ConversionRow
{
    public int OrdersPlaced { get; set; }
    public int OrdersCompleted { get; set; }
    public int OrdersCancelled { get; set; }
}

public class RecentOrderRow
{
    public Guid OrderId { get; set; }
    public long OrderCode { get; set; }
    public OrderStatus Status { get; set; }
    public decimal FinalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasPaidPayment { get; set; }
}

public class PromotionPerformanceRow
{
    public Guid PromotionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public PromotionStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int UsageCount { get; set; }
    public decimal DiscountTotal { get; set; }
}
