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

namespace InfrastructureLayer.Backgrounds
{
    public class OrderCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderCleanupBackgroundService> _logger;

        // Chu kỳ chạy dọn dẹp: Cứ mỗi 2 phút quét DB 1 lần
        private readonly TimeSpan _period = TimeSpan.FromMinutes(2);

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

            using PeriodicTimer timer = new PeriodicTimer(_period);

            // Vòng lặp sẽ chạy vô tận cho đến khi ứng dụng bị Stop (stoppingToken)
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanupExpiredOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[OrderCleanupService] Lỗi xảy ra khi dọn dẹp đơn hàng treo!");
                }
            }
        }

        private async Task CleanupExpiredOrdersAsync(CancellationToken stoppingToken)
        {
            // BackgroundService là Singleton, 
            // nên bắt buộc phải tạo Scope riêng để dùng DbContext (Scoped service)
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SNMDbContext>();

                // Mốc thời gian ngắt: Hiện tại trừ đi 15 phút
                var cutoffTime = DateTime.UtcNow.AddMinutes(-_orderTimeoutMinutes);

                // 1. Tìm tất cả đơn hàng Placed tạo trước mốc cutoffTime
                var expiredOrders = await dbContext.Orders
                    .Include(o => o.Payments)
                    .Where(o => (o.Status == OrderStatus.Placed)
                             && o.CreatedAt <= cutoffTime)
                    .ToListAsync(stoppingToken);

                if (expiredOrders.Any())
                {
                    _logger.LogInformation($"[OrderCleanupService] Phát hiện {expiredOrders.Count} đơn hàng quá hạn {_orderTimeoutMinutes} phút. Bắt đầu hủy...");

                    foreach (var order in expiredOrders)
                    {
                        // 2. Chuyển trạng thái đơn sang Cancelled
                        order.Status = OrderStatus.Cancelled;
                        order.UpdatedAt = DateTime.UtcNow;

                        // 3. Nếu có bản ghi Payment Pending tương ứng, hủy luôn Payment
                        var pendingPayment = order.Payments?.FirstOrDefault(p => p.Status == PaymentStatus.Pending);
                        if (pendingPayment != null)
                        {
                            pendingPayment.Status = PaymentStatus.Cancelled;
                            pendingPayment.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    // 4. Lưu thay đổi xuống Database
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation($"[OrderCleanupService] Đã hủy thành công {expiredOrders.Count} đơn hàng treo.");
                }
            }
        }
    }
}

