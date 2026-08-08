using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;
using DomainLayer.Common;
using ApplicationLayer.Services.PayOS;

namespace InfrastructureLayer.Backgrounds
{
    public class OrderCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderCleanupBackgroundService> _logger;

        // Chu kỳ chạy dọn dẹp: Cứ mỗi 2 phút quét DB 1 lần
        protected virtual TimeSpan Period => TimeSpan.FromMinutes(2);

        // Thời hạn hết hạn đơn hàng: 15 phút
        private readonly int _orderTimeoutMinutes = 15;

        public OrderCleanupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<OrderCleanupBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[OrderCleanupService] Background Service dọn dẹp đơn hàng đã bắt đầu chạy.");

            try
            {
                using PeriodicTimer timer = new PeriodicTimer(Period);

                // Vòng lặp sẽ chạy vô tận cho đến khi ứng dụng bị Stop (stoppingToken)
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await RunOnceAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[OrderCleanupService] Lỗi xảy ra khi dọn dẹp đơn hàng treo! Service sẽ thử lại ở chu kỳ kế tiếp.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("[OrderCleanupService] Đã dừng do host đang shutdown.");
            }
        }

        public virtual async Task RunOnceAsync(CancellationToken stoppingToken = default)
        {
            // BackgroundService là Singleton, 
            // nên bắt buộc phải tạo Scope riêng để dùng DbContext (Scoped service)
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
                var payos = scope.ServiceProvider.GetRequiredService<IPayOSService>();

                // Mốc thời gian ngắt: Hiện tại trừ đi 15 phút
                var cutoffTime = DateTime.UtcNow.AddMinutes(-_orderTimeoutMinutes);

                // 1. Tìm tất cả đơn hàng Placed tạo trước mốc cutoffTime
                await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);
                // Claim Order rows first, then reload their graph in a new
                // statement. PostgreSQL otherwise retains the pre-wait snapshot
                // for Include data while a competing webhook owns the row lock.
                var expiredOrderIds = await dbContext.Database
                    .SqlQuery<Guid>($@"
                        SELECT o.""Id"" AS ""Value""
                        FROM ""Order"" AS o
                        WHERE o.""Status"" IN ('Placed', 'PendingPayment', 'PaymentFailed', 'Underpaid')
                          AND o.""CreatedAt"" <= {cutoffTime}
                          AND NOT EXISTS (
                              SELECT 1 FROM ""Payments"" AS p
                              WHERE p.""OrderId"" = o.""Id""
                                AND p.""Status"" = 'Paid')
                        ORDER BY o.""CreatedAt""
                        FOR UPDATE OF o SKIP LOCKED
                        LIMIT 100")
                    .ToListAsync(stoppingToken);

                var expiredOrders = await dbContext.Orders
                    .Where(order => expiredOrderIds.Contains(order.Id))
                    .Include(o => o.Payments)
                    .Include(o => o.PromotionUsages)
                    .ToListAsync(stoppingToken);

                if (expiredOrders.Any())
                {
                    _logger.LogInformation($"[OrderCleanupService] Phát hiện {expiredOrders.Count} đơn hàng quá hạn {_orderTimeoutMinutes} phút. Bắt đầu hủy...");

                    var providerCodesToCancel = new List<long>();
                    foreach (var order in expiredOrders)
                    {
                        if (order.Payments.Any(payment => payment.Status == PaymentStatus.Paid))
                            continue;

                        // 2. Chuyển trạng thái đơn sang Cancelled
                        order.Status = OrderStatus.Cancelled;
                        order.UpdatedAt = DateTime.UtcNow;

                        PromotionUsageLifecycle.ReleaseReserved(
                            order.PromotionUsages,
                            order.UpdatedAt);

                        // 3. Nếu có bản ghi Payment Pending tương ứng, hủy luôn Payment
                        foreach (var pendingPayment in order.Payments.Where(p => p.Status == PaymentStatus.Pending))
                        {
                            pendingPayment.Status = PaymentStatus.Cancelled;
                            pendingPayment.UpdatedAt = DateTime.UtcNow;
                            if (pendingPayment.PayOSOrderCode.HasValue)
                                providerCodesToCancel.Add(pendingPayment.PayOSOrderCode.Value);
                        }

                        foreach (var underpaidPayment in order.Payments.Where(p => p.Status == PaymentStatus.Underpaid))
                        {
                            underpaidPayment.Status = PaymentStatus.RefundProcessing;
                            underpaidPayment.RefundAmount = underpaidPayment.Amount;
                            underpaidPayment.RefundReference = $"refund-{underpaidPayment.Id:N}";
                            underpaidPayment.RefundReason = "Underpaid order expired before fulfillment.";
                            underpaidPayment.RefundRequestedAt = DateTime.UtcNow;
                            underpaidPayment.UpdatedAt = underpaidPayment.RefundRequestedAt.Value;
                        }
                    }

                    // 4. Lưu thay đổi xuống Database
                    await dbContext.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);

                    foreach (var providerCode in providerCodesToCancel.Distinct())
                    {
                        try
                        {
                            await payos.CancelPaymentLinkAsync(providerCode);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            _logger.LogWarning(
                                exception,
                                "Expired order was cancelled locally but PayOS link {ProviderOrderCode} could not be cancelled.",
                                providerCode);
                        }
                    }

                    _logger.LogInformation($"[OrderCleanupService] Đã hủy thành công {expiredOrders.Count} đơn hàng treo.");
                }
                else
                {
                    await transaction.CommitAsync(stoppingToken);
                }
            }
        }
    }
}
