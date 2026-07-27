using System.Collections.Concurrent;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Orders;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.Promotions;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Backgrounds;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using PayOS.Models.Webhooks;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class PostgresBusinessConcurrencyTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task ConcurrentCheckout_GuaranteesHoldThroughRealOrderService()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection))
            return;

        var databaseName = $"snm_business_concurrency_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();

        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseNpgsql(testBuilder.ConnectionString)
            .Options;
        try
        {
            await using (var context = new SNMDbContext(options))
                await context.Database.MigrateAsync();
            var seed = await SeedAsync(testBuilder.ConnectionString);

            await AssertGlobalQuotaAsync(options, seed);
            await AssertCustomerHistorySnapshotsAsync(options, seed);
            await ResetCheckoutDataAsync(testBuilder.ConnectionString, totalLimit: 10, customerLimit: 1);
            await AssertCustomerQuotaAsync(options, seed);
            await ResetCheckoutDataAsync(testBuilder.ConnectionString, totalLimit: 10, customerLimit: 10);
            await AssertSameKeyIdempotencyAsync(options, seed);
            await ResetCheckoutDataAsync(testBuilder.ConnectionString, totalLimit: 10, customerLimit: 10);
            await AssertDifferentKeysRemainIndependentAsync(options, seed);
            await ResetCheckoutDataAsync(testBuilder.ConnectionString, totalLimit: 100, customerLimit: 100);
            await AssertRaceMatrixAsync(options, testBuilder.ConnectionString, seed);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString);
            await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", cleanup);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertGlobalQuotaAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var generator = new ConcurrentOrderCodeGenerator();
        var first = Request(seed, seed.CustomerA, Guid.NewGuid());
        var second = Request(seed, seed.CustomerB, Guid.NewGuid());
        var outcomes = await RunConcurrentlyAsync(
            () => CheckoutAsync(options, provider, generator, first),
            () => CheckoutAsync(options, provider, generator, second));

        Assert.True(
            outcomes.Count(outcome => outcome.Success) == 1,
            $"Expected one success; outcomes: {string.Join(", ", outcomes.Select(value => value.ErrorCode ?? "SUCCESS"))}");
        Assert.Contains(outcomes, outcome => outcome.ErrorCode == "PROMOTION_USAGE_LIMIT_REACHED");
        await using var context = new SNMDbContext(options);
        Assert.Equal(1, await context.Orders.CountAsync());
        Assert.Equal(1, await context.PromotionUsages.CountAsync(usage => usage.Status != PromotionUsageStatus.Released));
        Assert.Equal(1, provider.CreateCount);
    }

    private static async Task AssertCustomerQuotaAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var generator = new ConcurrentOrderCodeGenerator();
        var outcomes = await RunConcurrentlyAsync(
            () => CheckoutAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid())),
            () => CheckoutAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid())));

        Assert.Equal(1, outcomes.Count(outcome => outcome.Success));
        Assert.Contains(outcomes, outcome => outcome.ErrorCode == "PROMOTION_CUSTOMER_LIMIT_REACHED");
        await using var context = new SNMDbContext(options);
        Assert.Equal(1, await context.Orders.CountAsync());
        Assert.Equal(1, await context.PromotionUsages.CountAsync());
        Assert.Equal(1, provider.CreateCount);
    }

    private static async Task AssertCustomerHistorySnapshotsAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        Guid customerId;
        Guid orderId;
        await using (var context = new SNMDbContext(options))
        {
            var order = await context.Orders
                .Include(value => value.OrderDetails)
                .Include(value => value.PromotionUsages)
                .SingleAsync();
            customerId = order.CustomerId;
            orderId = order.Id;
            Assert.Equal("Concurrency food", Assert.Single(order.OrderDetails).FoodNameSnapshot);
            Assert.Equal(100_000m, order.OrderDetails.Single().UnitPrice);
            Assert.Equal("CONCURRENT", Assert.Single(order.PromotionUsages).PromotionCodeSnapshot);
            Assert.Equal("Concurrent promotion", order.PromotionUsages.Single().PromotionTitleSnapshot);

            var food = await context.FoodItems.SingleAsync(value => value.Id == seed.Food);
            food.Name = "Renamed food";
            food.Price = 777_000m;
            var promotion = await context.Promotions.SingleAsync(value => value.Id == seed.Promotion);
            promotion.PromotionCode = "RENAMED";
            promotion.Title = "Renamed promotion";
            await context.SaveChangesAsync();
        }

        await using (var historyContext = new SNMDbContext(options))
        {
            var detail = await new OrderRepository(historyContext).GetCustomerDetailAsync(customerId, orderId);
            Assert.NotNull(detail);
            Assert.Equal("Concurrency food", Assert.Single(detail!.Items).FoodName);
            Assert.Equal(100_000m, detail.Items.Single().UnitPrice);
            Assert.Equal(100_000m, detail.Subtotal);
            Assert.Equal(10_000m, detail.DiscountAmount);
            Assert.Equal(90_000m, detail.FinalAmount);
            Assert.Equal("CONCURRENT", detail.Promotion!.PromotionCode);
            Assert.Equal("Concurrent promotion", detail.Promotion.PromotionTitle);
        }

        await using (var restore = new SNMDbContext(options))
        {
            var food = await restore.FoodItems.SingleAsync(value => value.Id == seed.Food);
            food.Name = "Concurrency food";
            food.Price = 100_000m;
            var promotion = await restore.Promotions.SingleAsync(value => value.Id == seed.Promotion);
            promotion.PromotionCode = "CONCURRENT";
            promotion.Title = "Concurrent promotion";
            await restore.SaveChangesAsync();
        }
    }

    private static async Task AssertSameKeyIdempotencyAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var generator = new ConcurrentOrderCodeGenerator();
        var key = Guid.NewGuid();
        var outcomes = await RunConcurrentlyAsync(
            () => CheckoutAsync(options, provider, generator, Request(seed, seed.CustomerA, key)),
            () => CheckoutAsync(options, provider, generator, Request(seed, seed.CustomerA, key)));

        Assert.All(outcomes, outcome => Assert.True(outcome.Success));
        Assert.Single(outcomes.Select(outcome => outcome.OrderId).Distinct());
        await using var context = new SNMDbContext(options);
        Assert.Equal(1, await context.Orders.CountAsync());
        Assert.Equal(1, await context.Payments.CountAsync());
        Assert.Equal(1, await context.PromotionUsages.CountAsync());
        Assert.Equal(1, provider.CreateCount);
    }

    private static async Task AssertDifferentKeysRemainIndependentAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var generator = new ConcurrentOrderCodeGenerator();
        var outcomes = await RunConcurrentlyAsync(
            () => CheckoutAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid())),
            () => CheckoutAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid())));

        Assert.All(outcomes, outcome => Assert.True(outcome.Success));
        Assert.Equal(2, outcomes.Select(outcome => outcome.OrderId).Distinct().Count());
        await using var context = new SNMDbContext(options);
        Assert.Equal(2, await context.Orders.CountAsync());
        Assert.Equal(2, await context.Payments.CountAsync());
        Assert.Equal(2, await context.PromotionUsages.CountAsync());
        Assert.Equal(2, provider.CreateCount);
    }

    private static async Task AssertRaceMatrixAsync(
        DbContextOptions<SNMDbContext> options,
        string connectionString,
        SeedIds seed)
    {
        await AssertWebhookVsCleanupAsync(options, connectionString, seed);
        await ResetCheckoutDataAsync(connectionString, 100, 100);
        await AssertWebhookVsCustomerCancelAsync(options, seed);
        await ResetCheckoutDataAsync(connectionString, 100, 100);
        await AssertWebhookVsBoothCancelAsync(options, seed);
        await ResetCheckoutDataAsync(connectionString, 100, 100);
        await AssertDuplicateWebhookAsync(options, seed);
        await ResetCheckoutDataAsync(connectionString, 100, 100);
        await AssertTwoCleanupWorkersAsync(options, connectionString, seed);
        await ResetCheckoutDataAsync(connectionString, 100, 100);
        await AssertPayoutConcurrencyAsync(options, seed);
    }

    private static async Task AssertWebhookVsCleanupAsync(
        DbContextOptions<SNMDbContext> options,
        string connectionString,
        SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var generator = new ConcurrentOrderCodeGenerator();
        var created = await CreatePendingOrderAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid()));
        await BackdateOrderAsync(connectionString, created.OrderId!.Value);
        await using var webhookScope = CreateOrderService(options, provider, generator);
        using var cleanupProvider = CleanupProvider(options, provider);
        var cleanup = new OrderCleanupBackgroundService(cleanupProvider, NullLogger<OrderCleanupBackgroundService>.Instance);

        using var gate = new ManualResetEventSlim(false);
        var webhook = Task.Run(async () =>
        {
            gate.Wait();
            return await webhookScope.Service.ProcessPaymentWebhookAsync(Webhook(created));
        });
        var cleanupTask = Task.Run(async () =>
        {
            gate.Wait();
            await cleanup.RunOnceAsync();
        });
        gate.Set();
        await Task.WhenAll(webhook, cleanupTask);

        await using var context = new SNMDbContext(options);
        var order = await context.Orders.Include(value => value.Payments).Include(value => value.PromotionUsages)
            .SingleAsync(value => value.Id == created.OrderId);
        var payment = Assert.Single(order.Payments);
        var usage = Assert.Single(order.PromotionUsages);
        var webhookWon = order.Status == OrderStatus.Preparing
            && payment.Status == PaymentStatus.Paid
            && usage.Status == PromotionUsageStatus.Consumed;
        var cleanupWon = order.Status == OrderStatus.Cancelled
            && payment.Status == PaymentStatus.RefundProcessing
            && usage.Status == PromotionUsageStatus.Released;
        Assert.True(webhookWon || cleanupWon, $"Unsafe cleanup race: {order.Status}/{payment.Status}/{usage.Status}");
    }

    private static async Task AssertWebhookVsCustomerCancelAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var generator = new ConcurrentOrderCodeGenerator();
        var created = await CreatePendingOrderAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid()));
        await using var webhookScope = CreateOrderService(options, provider, generator);
        await using var cancelScope = CreateOrderService(options, provider, generator);
        using var gate = new ManualResetEventSlim(false);
        var webhook = Task.Run(async () => { gate.Wait(); return await webhookScope.Service.ProcessPaymentWebhookAsync(Webhook(created)); });
        var cancel = Task.Run(async () => { gate.Wait(); return await cancelScope.Service.CancelOrderByCustomer(seed.CustomerA, created.OrderCode); });
        gate.Set();
        await Task.WhenAll(webhook, cancel);

        await AssertPaidOrRecoverableAsync(
            options,
            created.OrderId!.Value,
            $"webhook={webhook.Result}, cancel={cancel.Result.Success}/{cancel.Result.ErrorCode}");
    }

    private static async Task AssertWebhookVsBoothCancelAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var payouts = new RecordingPayoutService();
        var generator = new ConcurrentOrderCodeGenerator();
        var created = await CreatePendingOrderAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid()));
        await using var webhookScope = CreateOrderService(options, provider, generator, payouts);
        await using var cancelScope = CreateOrderService(options, provider, generator, payouts);
        using var gate = new ManualResetEventSlim(false);
        var webhook = Task.Run(async () => { gate.Wait(); return await webhookScope.Service.ProcessPaymentWebhookAsync(Webhook(created)); });
        var cancel = Task.Run(async () =>
        {
            gate.Wait();
            return await cancelScope.Service.CancelOrderByBoothOwnerAsync(
                seed.Owner, created.OrderCode,
                new RefundQRRequest { BankBin = "970436", AccountNumber = "123456", RefundReason = "Race" });
        });
        gate.Set();
        await Task.WhenAll(webhook, cancel);

        await AssertPaidOrRecoverableAsync(
            options,
            created.OrderId!.Value,
            $"webhook={webhook.Result}, cancel={cancel.Result.Success}/{cancel.Result.ErrorCode}");
        Assert.InRange(payouts.CreateCount, 0, 1);
    }

    private static async Task AssertDuplicateWebhookAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var notifications = new RecordingNotificationPublisher();
        var generator = new ConcurrentOrderCodeGenerator();
        var created = await CreatePendingOrderAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid()));
        await using var first = CreateOrderService(options, provider, generator, notifications: notifications);
        await using var second = CreateOrderService(options, provider, generator, notifications: notifications);
        using var gate = new ManualResetEventSlim(false);
        var tasks = new[]
        {
            Task.Run(async () => { gate.Wait(); return await first.Service.ProcessPaymentWebhookAsync(Webhook(created)); }),
            Task.Run(async () => { gate.Wait(); return await second.Service.ProcessPaymentWebhookAsync(Webhook(created)); })
        };
        gate.Set();
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(result => result == WebhookDispatchResult.OrderHandled));
        Assert.Equal(1, results.Count(result => result == WebhookDispatchResult.AlreadyProcessed));

        await using var context = new SNMDbContext(options);
        var order = await context.Orders.Include(value => value.Payments).Include(value => value.PromotionUsages)
            .SingleAsync(value => value.Id == created.OrderId);
        Assert.Equal(OrderStatus.Preparing, order.Status);
        Assert.Equal(PaymentStatus.Paid, Assert.Single(order.Payments).Status);
        Assert.Equal(PromotionUsageStatus.Consumed, Assert.Single(order.PromotionUsages).Status);
        Assert.Equal(1, notifications.PublishCount);
    }

    private static async Task AssertTwoCleanupWorkersAsync(
        DbContextOptions<SNMDbContext> options,
        string connectionString,
        SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var generator = new ConcurrentOrderCodeGenerator();
        var orders = new List<CheckoutOutcome>();
        for (var index = 0; index < 4; index++)
        {
            var created = await CreatePendingOrderAsync(
                options, provider, generator,
                Request(seed, index % 2 == 0 ? seed.CustomerA : seed.CustomerB, Guid.NewGuid()));
            orders.Add(created);
            await BackdateOrderAsync(connectionString, created.OrderId!.Value);
        }

        using var firstProvider = CleanupProvider(options, provider);
        using var secondProvider = CleanupProvider(options, provider);
        var first = new OrderCleanupBackgroundService(firstProvider, NullLogger<OrderCleanupBackgroundService>.Instance);
        var second = new OrderCleanupBackgroundService(secondProvider, NullLogger<OrderCleanupBackgroundService>.Instance);
        await Task.WhenAll(first.RunOnceAsync(), second.RunOnceAsync());

        await using var context = new SNMDbContext(options);
        var finalOrders = await context.Orders.Include(value => value.Payments).Include(value => value.PromotionUsages).ToListAsync();
        Assert.Equal(4, finalOrders.Count);
        Assert.All(finalOrders, order =>
        {
            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(PaymentStatus.Cancelled, Assert.Single(order.Payments).Status);
            Assert.Equal(PromotionUsageStatus.Released, Assert.Single(order.PromotionUsages).Status);
        });
        Assert.All(provider.CancelCounts.Values, count => Assert.Equal(1, count));
        Assert.Equal(4, provider.CancelCounts.Count);
    }

    private static async Task AssertPayoutConcurrencyAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var provider = new RecordingPayOSService();
        var payouts = new RecordingPayoutService();
        var generator = new ConcurrentOrderCodeGenerator();
        var created = await CreatePendingOrderAsync(options, provider, generator, Request(seed, seed.CustomerA, Guid.NewGuid()));
        await using (var webhookScope = CreateOrderService(options, provider, generator, payouts))
            Assert.Equal(WebhookDispatchResult.OrderHandled, await webhookScope.Service.ProcessPaymentWebhookAsync(Webhook(created)));

        await using var first = CreateOrderService(options, provider, generator, payouts);
        await using var second = CreateOrderService(options, provider, generator, payouts);
        var refundRequest = new RefundQRRequest { BankBin = "970436", AccountNumber = "123456", RefundReason = "Concurrent refund" };
        using var gate = new ManualResetEventSlim(false);
        var dispatches = new[]
        {
            Task.Run(async () => { gate.Wait(); return await first.Service.CancelOrderByBoothOwnerAsync(seed.Owner, created.OrderCode, refundRequest); }),
            Task.Run(async () => { gate.Wait(); return await second.Service.CancelOrderByBoothOwnerAsync(seed.Owner, created.OrderCode, refundRequest); })
        };
        gate.Set();
        await Task.WhenAll(dispatches);
        Assert.Equal(1, payouts.CreateCount);

        payouts.Outcome = PayOSPayoutOutcome.Succeeded;
        await using var reconcileA = CreateOrderService(options, provider, generator, payouts);
        await using var reconcileB = CreateOrderService(options, provider, generator, payouts);
        var reconciliations = await Task.WhenAll(
            reconcileA.Service.ReconcileRefundAsync(seed.Owner, created.OrderCode),
            reconcileB.Service.ReconcileRefundAsync(seed.Owner, created.OrderCode));
        Assert.All(reconciliations, response => Assert.True(response.Success));

        await using var context = new SNMDbContext(options);
        var payment = await context.Payments.SingleAsync(value => value.OrderId == created.OrderId);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(90_000m, payment.RefundAmount);
        Assert.Equal($"refund-{payment.Id:N}", payment.RefundReference);
        Assert.Equal($"payout-{payment.Id:N}", payment.PayoutId);
    }

    private static async Task AssertPaidOrRecoverableAsync(
        DbContextOptions<SNMDbContext> options,
        Guid orderId,
        string? diagnostic = null)
    {
        await using var context = new SNMDbContext(options);
        var order = await context.Orders.Include(value => value.Payments).Include(value => value.PromotionUsages)
            .SingleAsync(value => value.Id == orderId);
        var payment = Assert.Single(order.Payments);
        var usage = Assert.Single(order.PromotionUsages);
        var paid = order.Status == OrderStatus.Preparing
            && payment.Status == PaymentStatus.Paid
            && usage.Status == PromotionUsageStatus.Consumed;
        var recoverable = order.Status == OrderStatus.Cancelled
            && payment.Status == PaymentStatus.RefundProcessing
            && payment.RefundAmount == payment.Amount;
        Assert.True(paid || recoverable, $"Unsafe final state: {order.Status}/{payment.Status}/{usage.Status}; {diagnostic}");
    }

    private static async Task<CheckoutOutcome> CreatePendingOrderAsync(
        DbContextOptions<SNMDbContext> options,
        RecordingPayOSService provider,
        IPayOSOrderCodeGenerator generator,
        CreateOrderDto request)
    {
        var result = await CheckoutAsync(options, provider, generator, request);
        Assert.True(result.Success, result.ErrorCode);
        await using var context = new SNMDbContext(options);
        var order = await context.Orders.SingleAsync(value => value.Id == result.OrderId);
        return result with { OrderCode = order.OrderCode };
    }

    private static PayOSWebhookData Webhook(CheckoutOutcome created)
        => new()
        {
            OrderCode = created.OrderCode,
            Amount = 90_000m,
            IsSuccessful = true,
            Code = "00",
            PaymentLinkId = $"link-{created.OrderCode}",
            Reference = "bank-reference"
        };

    private static async Task BackdateOrderAsync(string connectionString, Guid orderId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE \"Order\" SET \"CreatedAt\" = now() - interval '30 minutes' WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", orderId);
        await command.ExecuteNonQueryAsync();
    }

    private static ServiceProvider CleanupProvider(DbContextOptions<SNMDbContext> options, IPayOSService provider)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new SNMDbContext(options));
        services.AddSingleton(provider);
        services.AddSingleton<IPayOSService>(provider);
        return services.BuildServiceProvider();
    }

    private static async Task<CheckoutOutcome> CheckoutAsync(
        DbContextOptions<SNMDbContext> options,
        RecordingPayOSService provider,
        IPayOSOrderCodeGenerator generator,
        CreateOrderDto request)
    {
        await using var scope = CreateOrderService(options, provider, generator);
        try
        {
            var response = await scope.Service.CreateOrderAsync(request);
            return new CheckoutOutcome(true, null, response.Data!.OrderId);
        }
        catch (AppException exception)
        {
            return new CheckoutOutcome(false, exception.ErrorCode, null);
        }
    }

    private static async Task<CheckoutOutcome[]> RunConcurrentlyAsync(
        Func<Task<CheckoutOutcome>> first,
        Func<Task<CheckoutOutcome>> second)
    {
        using var gate = new ManualResetEventSlim(false);
        var tasks = new[]
        {
            Task.Run(async () => { gate.Wait(); return await first(); }),
            Task.Run(async () => { gate.Wait(); return await second(); })
        };
        gate.Set();
        return await Task.WhenAll(tasks);
    }

    private static OrderServiceScope CreateOrderService(
        DbContextOptions<SNMDbContext> options,
        IPayOSService provider,
        IPayOSOrderCodeGenerator generator,
        IPayOSPayoutService? payouts = null,
        IRealtimeNotificationPublisher? notifications = null)
    {
        var context = new SNMDbContext(options);
        var orders = new OrderRepository(context);
        var promotions = new PromotionRepository(context);
        var usages = new PromotionUsageRepository(context);
        var service = new OrderService(
            orders,
            promotions,
            new PromotionValidationService(usages, TimeProvider.System),
            payouts ?? Mock.Of<IPayOSPayoutService>(),
            notifications ?? Mock.Of<IRealtimeNotificationPublisher>(),
            new FoodItemRepository(context),
            Mock.Of<ILogger<OrderService>>(),
            new ConfigurationBuilder().Build(),
            provider,
            generator,
            new BoothRepository(context),
            usages);
        return new OrderServiceScope(context, service);
    }

    private static CreateOrderDto Request(SeedIds seed, Guid customerId, Guid checkoutId)
        => new()
        {
            CheckoutRequestId = checkoutId,
            CustomerId = customerId,
            BoothId = seed.Booth,
            BoothOwnerId = seed.Owner,
            PaymentMethod = PaymentType.PayOS,
            PromotionCode = "CONCURRENT",
            Items = [new CartItemDto { FoodItemId = seed.Food, Quantity = 1, UnitPrice = 100_000m }]
        };

    private static async Task<SeedIds> SeedAsync(string connectionString)
    {
        var ids = new SeedIds(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "NightMarket"
                ("Id", "Name", "Address", "OpeningHours", "ClosingHours", "Status",
                 "ModerationStatus", "IsDeleted", "TotalBooth", "CreatedAt", "UpdatedAt")
            VALUES (@market, 'Concurrency market', 'Test', '00:00', '23:59:59', 'Open',
                    'Active', false, 1, now(), now());
            INSERT INTO "User"
                ("Id", "RoleId", "UserName", "PasswordHash", "FullName", "Email",
                 "AuthProvider", "Status", "CreatedAt", "UpdatedAt")
            VALUES
                (@owner, @role, 'owner-concurrency', 'hash', 'Owner', 'owner-concurrency@test.local', 'Local', 'Active', now(), now()),
                (@customerA, @role, 'customer-a-concurrency', 'hash', 'Customer A', 'customer-a-concurrency@test.local', 'Local', 'Active', now(), now()),
                (@customerB, @role, 'customer-b-concurrency', 'hash', 'Customer B', 'customer-b-concurrency@test.local', 'Local', 'Active', now(), now());
            INSERT INTO "Booth"
                ("Id", "RegistrationId", "NightMarketId", "BoothOwnerId", "BoothName", "Status", "CreatedAt", "UpdatedAt")
            VALUES (@booth, @registration, @market, @owner, 'Concurrency booth', 'Active', now(), now());
            INSERT INTO "FoodCategories"
                ("Id", "BoothId", "Name", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (@category, @booth, 'Category', false, now(), now());
            INSERT INTO "FoodItem"
                ("Id", "BoothId", "CategoryId", "Name", "Price", "IsAvailable", "IsFeatured", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (@food, @booth, @category, 'Concurrency food', 100000, true, false, false, now(), now());
            INSERT INTO "Promotion"
                ("Id", "BoothId", "PromotionCode", "Title", "DiscountType", "Scope", "DiscountValue",
                 "TotalUsageLimit", "UsageLimitPerCustomer", "IsPublic", "StartDate", "EndDate", "Status", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (@promotion, @booth, 'CONCURRENT', 'Concurrent promotion', 'FixedAmount', 'EntireBoothOrder', 10000,
                    1, 1, true, now() - interval '1 day', now() + interval '1 day', 'Active', false, now(), now());
            """, connection);
        command.Parameters.AddWithValue("market", ids.Market);
        command.Parameters.AddWithValue("owner", ids.Owner);
        command.Parameters.AddWithValue("customerA", ids.CustomerA);
        command.Parameters.AddWithValue("customerB", ids.CustomerB);
        command.Parameters.AddWithValue("role", Guid.NewGuid());
        command.Parameters.AddWithValue("booth", ids.Booth);
        command.Parameters.AddWithValue("registration", Guid.NewGuid());
        command.Parameters.AddWithValue("category", ids.Category);
        command.Parameters.AddWithValue("food", ids.Food);
        command.Parameters.AddWithValue("promotion", ids.Promotion);
        await command.ExecuteNonQueryAsync();
        return ids;
    }

    private static async Task ResetCheckoutDataAsync(string connectionString, int totalLimit, int customerLimit)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            TRUNCATE TABLE "PromotionUsages", "Payments", "OrderDetail", "Order" CASCADE;
            UPDATE "Promotion"
            SET "TotalUsageLimit" = @total, "UsageLimitPerCustomer" = @customer;
            """, connection);
        command.Parameters.AddWithValue("total", totalLimit);
        command.Parameters.AddWithValue("customer", customerLimit);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record SeedIds(
        Guid Market, Guid Owner, Guid CustomerA, Guid CustomerB,
        Guid Booth, Guid Category, Guid Food, Guid Promotion);

    private sealed record CheckoutOutcome(bool Success, string? ErrorCode, Guid? OrderId, long OrderCode = 0);

    private sealed class OrderServiceScope(SNMDbContext context, OrderService service) : IAsyncDisposable
    {
        public OrderService Service { get; } = service;
        public ValueTask DisposeAsync() => context.DisposeAsync();
    }

    private sealed class ConcurrentOrderCodeGenerator : IPayOSOrderCodeGenerator
    {
        private long _value = 100_000_000_500_000;
        public Task<long> GenerateAsync(PayOSOrderSource source) => Task.FromResult(Interlocked.Increment(ref _value));
        public PayOSOrderSource? GetSource(long orderCode) => PayOSOrderSource.Order;
    }

    private sealed class RecordingPayOSService : IPayOSService
    {
        private int _createCount;
        public int CreateCount => Volatile.Read(ref _createCount);
        public ConcurrentDictionary<long, int> CancelCounts { get; } = new();

        public async Task<PayOSPaymentResponse> CreatePaymentLinkAsync(PayOSPaymentRequest request)
        {
            Interlocked.Increment(ref _createCount);
            await Task.Delay(25);
            return new PayOSPaymentResponse
            {
                OrderCode = request.OrderCode,
                PaymentLinkId = $"link-{request.OrderCode}",
                CheckoutUrl = $"https://pay.test/{request.OrderCode}",
                Amount = request.Amount,
                Status = "PENDING"
            };
        }

        public Task CancelPaymentLinkAsync(long orderCode)
        {
            CancelCounts.AddOrUpdate(orderCode, 1, (_, count) => count + 1);
            return Task.CompletedTask;
        }

        public Task<PayOSWebhookData?> VerifyWebhookAsync(Webhook webhook) => Task.FromResult<PayOSWebhookData?>(null);
        public Task<PayOSPaymentStatus?> GetPaymentStatusAsync(long orderCode) => Task.FromResult<PayOSPaymentStatus?>(null);
    }

    private sealed class RecordingNotificationPublisher : IRealtimeNotificationPublisher
    {
        private int _publishCount;
        public int PublishCount => Volatile.Read(ref _publishCount);

        public Task PublishAsync(
            Guid userId,
            ApplicationLayer.DTOs.Responses.NotificationListItemResponse notification,
            int unreadCount,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _publishCount);
            return Task.CompletedTask;
        }

        public Task PublishUnreadCountAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingPayoutService : IPayOSPayoutService
    {
        private readonly ConcurrentDictionary<string, PayOSPayoutSnapshot> _created = new();
        private int _createCount;
        public int CreateCount => Volatile.Read(ref _createCount);
        public PayOSPayoutOutcome Outcome { get; set; } = PayOSPayoutOutcome.Processing;

        public async Task<PayOSPayoutSnapshot> CreateAsync(
            PayOSPayoutCommand command,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _createCount);
            await Task.Delay(50, cancellationToken);
            var paymentPart = command.ReferenceId.StartsWith("refund-", StringComparison.Ordinal)
                ? command.ReferenceId[7..]
                : command.ReferenceId;
            var snapshot = new PayOSPayoutSnapshot(
                $"payout-{paymentPart}", command.ReferenceId, command.Amount, Outcome);
            _created[snapshot.PayoutId] = snapshot;
            return snapshot;
        }

        public Task<PayOSPayoutSnapshot?> FindByReferenceAsync(
            string referenceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<PayOSPayoutSnapshot?>(null);

        public Task<PayOSPayoutSnapshot> GetAsync(string payoutId, CancellationToken cancellationToken = default)
        {
            var stored = _created[payoutId];
            return Task.FromResult(stored with { Outcome = Outcome });
        }
    }
}
