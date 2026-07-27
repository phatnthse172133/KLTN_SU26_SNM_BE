using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Complaints;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Reviews;
using ApplicationLayer.Services.Subscriptions;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit.Abstractions;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class PostgresCustomerHistoryReviewComplaintVerificationTests
{
    private const string MigrationId = "20260727044112_CompleteCustomerHistoryReviewComplaint";
    private readonly ITestOutputHelper _output;

    public PostgresCustomerHistoryReviewComplaintVerificationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task AppliedDatabase_HasMigrationBackfillAndExpectedIndexes()
    {
        var connectionString = Environment.GetEnvironmentVariable("SNM_APPLIED_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Assert.Equal(1L, await ScalarLongAsync(connection,
            "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @migration", ("migration", MigrationId)));
        var orderDetailCount = await ScalarLongAsync(connection, "SELECT COUNT(*) FROM \"OrderDetail\"");
        var invalidFoodSnapshots = await ScalarLongAsync(connection,
            "SELECT COUNT(*) FROM \"OrderDetail\" WHERE \"FoodNameSnapshot\" IS NULL OR btrim(\"FoodNameSnapshot\") = ''");
        var usageCount = await ScalarLongAsync(connection, "SELECT COUNT(*) FROM \"PromotionUsages\"");
        var invalidPromotionSnapshots = await ScalarLongAsync(connection,
            "SELECT COUNT(*) FROM \"PromotionUsages\" WHERE \"PromotionTitleSnapshot\" IS NULL OR btrim(\"PromotionTitleSnapshot\") = ''");
        Assert.Equal(0, invalidFoodSnapshots);
        Assert.Equal(0, invalidPromotionSnapshots);

        var reviewIndex = await ScalarStringAsync(connection,
            "SELECT indexdef FROM pg_indexes WHERE indexname = 'uq_review_order'");
        Assert.Contains("UNIQUE", reviewIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OrderId", reviewIndex, StringComparison.Ordinal);

        var complaintIndex = await ScalarStringAsync(connection,
            "SELECT indexdef FROM pg_indexes WHERE indexname = 'uq_complaint_active_customer_order_booth'");
        Assert.Contains("UNIQUE", complaintIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomerId", complaintIndex, StringComparison.Ordinal);
        Assert.Contains("OrderId", complaintIndex, StringComparison.Ordinal);
        Assert.Contains("BoothId", complaintIndex, StringComparison.Ordinal);
        Assert.Contains("Status", complaintIndex, StringComparison.Ordinal);
        Assert.Contains("Pending", complaintIndex, StringComparison.Ordinal);

        _output.WriteLine($"Applied DB legacy rows: OrderDetail={orderDetailCount}, PromotionUsage={usageCount}; invalid snapshots=0/0.");
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task DisposablePostgres_EnforcesReviewComplaintHistoryInvariants()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection))
            return;

        var databaseName = $"snm_history_review_complaint_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();

        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };
        var options = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
        try
        {
            await using (var migration = new SNMDbContext(options))
            {
                var migrator = migration.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20260727034827_OptimizeChatNotificationIndexes");
                var legacy = await SeedLegacySnapshotsAsync(testBuilder.ConnectionString);
                await migrator.MigrateAsync(MigrationId);
                await AssertLegacyBackfillAsync(testBuilder.ConnectionString, legacy);
            }
            var seed = await SeedAsync(testBuilder.ConnectionString);

            var reviewId = await AssertReviewConstraintsAndConcurrencyAsync(options, testBuilder.ConnectionString, seed);
            await AssertAverageReplyNotificationAndIdorAsync(options, seed, reviewId);
            await AssertComplaintConstraintAuthorityMoneyAndIdorAsync(options, seed);
            await AssertHistoryPaymentRefundAndIdorAsync(options, seed);
            await AssertStablePaginationAsync(options, seed);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString);
            await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", cleanup);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid> AssertReviewConstraintsAndConcurrencyAsync(
        DbContextOptions<SNMDbContext> options, string connectionString, SeedIds seed)
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        using var gate = new ManualResetEventSlim(false);
        async Task<bool> CreateAsync(Guid reviewId)
        {
            await using var context = new SNMDbContext(options);
            var repository = new ReviewRepository(context);
            await repository.AddAsync(NewReview(reviewId, seed.CustomerA, seed.OrderReviewA, seed.Booth, 5));
            gate.Wait();
            return await repository.TrySaveNewReviewAsync();
        }

        var attempts = new[] { Task.Run(() => CreateAsync(firstId)), Task.Run(() => CreateAsync(secondId)) };
        gate.Set();
        var results = await Task.WhenAll(attempts);
        Assert.Equal(1, results.Count(value => value));
        Assert.Equal(1, results.Count(value => !value));

        await using var check = new SNMDbContext(options);
        var persisted = await check.Reviews.SingleAsync(value => value.OrderId == seed.OrderReviewA);
        Assert.Equal((short)5, persisted.Rating);

        await AssertRatingRejectedAsync(connectionString, seed, seed.OrderInvalidLow, 0);
        await AssertRatingRejectedAsync(connectionString, seed, seed.OrderInvalidHigh, 6);

        check.Reviews.Add(NewReview(Guid.NewGuid(), seed.CustomerA, seed.OrderReviewB, seed.Booth, 3));
        await check.SaveChangesAsync();
        return persisted.Id;
    }

    private static async Task AssertAverageReplyNotificationAndIdorAsync(
        DbContextOptions<SNMDbContext> options, SeedIds seed, Guid reviewAId)
    {
        await using var context = new SNMDbContext(options);
        var reviews = new ReviewRepository(context);
        await reviews.RefreshBoothAverageRatingAsync(seed.Booth);
        Assert.Equal(4m, (await context.Booths.SingleAsync(value => value.Id == seed.Booth)).AverageRating);

        var reviewB = await context.Reviews.SingleAsync(value => value.OrderId == seed.OrderReviewB);
        reviewB.Rating = 1;
        await context.SaveChangesAsync();
        await reviews.RefreshBoothAverageRatingAsync(seed.Booth);
        Assert.Equal(3m, (await context.Booths.SingleAsync(value => value.Id == seed.Booth)).AverageRating);

        var reviewA = await context.Reviews.SingleAsync(value => value.Id == reviewAId);
        reviewA.IsVisible = false;
        await context.SaveChangesAsync();
        await reviews.RefreshBoothAverageRatingAsync(seed.Booth);
        Assert.Equal(1m, (await context.Booths.SingleAsync(value => value.Id == seed.Booth)).AverageRating);

        var notificationService = PersistentNotificationService(context);
        var mapper = new Mock<IMapper>();
        mapper.Setup(value => value.Map<ReviewResponse>(It.IsAny<Review>())).Returns(new ReviewResponse());
        var entitlements = new Mock<ISubscriptionEntitlementService>();
        entitlements.Setup(value => value.RequireBoothFeatureAsync(
                seed.Booth, It.IsAny<Func<BoothEntitlements, bool>>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var service = new ReviewService(reviews, new BoothRepository(context), new OrderRepository(context),
            mapper.Object, notificationService, entitlements.Object);

        var request = new UpsertReviewReplyRequest { Content = "Thank you for your feedback." };
        await service.UpsertReplyAsync(seed.Owner, reviewAId, request);
        await service.UpsertReplyAsync(seed.Owner, reviewAId, request);
        Assert.Equal(1, await context.ReviewReplies.CountAsync(value => value.ReviewId == reviewAId));
        Assert.Equal(1, await context.Notifications.CountAsync(value =>
            value.UserId == seed.CustomerA && value.Type == NotificationType.ReviewReplied && value.ReferenceId == reviewAId));

        var forbidden = await Assert.ThrowsAsync<AppException>(() =>
            service.UpsertReplyAsync(seed.ForeignOwner, reviewAId, new UpsertReviewReplyRequest { Content = "Unauthorized" }));
        Assert.Equal(403, forbidden.StatusCode);

        var hidden = await Assert.ThrowsAsync<AppException>(() =>
            service.UpdateAsync(seed.CustomerB, reviewAId, new UpdateReviewRequest { Rating = 4 }));
        Assert.Equal(404, hidden.StatusCode);
    }

    private static async Task AssertComplaintConstraintAuthorityMoneyAndIdorAsync(
        DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        using var gate = new ManualResetEventSlim(false);
        async Task<bool> CreateDuplicateAsync()
        {
            await using var context = new SNMDbContext(options);
            var repository = new ComplaintRepository(context);
            await repository.AddAsync(NewComplaint(seed.CustomerA, seed.OrderComplaintConcurrent, seed.Booth));
            gate.Wait();
            return await repository.TrySaveNewComplaintAsync();
        }

        var attempts = new[] { Task.Run(CreateDuplicateAsync), Task.Run(CreateDuplicateAsync) };
        gate.Set();
        var results = await Task.WhenAll(attempts);
        Assert.Equal(1, results.Count(value => value));
        Assert.Equal(1, results.Count(value => !value));

        await using var context = new SNMDbContext(options);
        Assert.Equal(1, await context.Complaints.CountAsync(value => value.OrderId == seed.OrderComplaintConcurrent));
        await context.Complaints.Where(value => value.OrderId == seed.OrderComplaintConcurrent)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, ComplaintStatus.Resolved));
        var later = new ComplaintRepository(context);
        await later.AddAsync(NewComplaint(seed.CustomerA, seed.OrderComplaintConcurrent, seed.Booth));
        Assert.True(await later.TrySaveNewComplaintAsync());
        Assert.Equal(2, await context.Complaints.CountAsync(value => value.OrderId == seed.OrderComplaintConcurrent));

        var paymentCount = await context.Payments.CountAsync();
        var usageCount = await context.PromotionUsages.CountAsync();
        var orderBefore = await context.Orders.AsNoTracking().SingleAsync(value => value.Id == seed.OrderComplaintService);

        var mapper = new Mock<IMapper>();
        mapper.Setup(value => value.Map<Complaint>(It.IsAny<CreateComplaintRequest>()))
            .Returns((CreateComplaintRequest request) => new Complaint
            {
                OrderId = request.OrderId, BoothId = request.BoothId,
                Title = request.Title.Trim(), Description = request.Description.Trim()
            });
        mapper.Setup(value => value.Map<ComplaintResponse>(It.IsAny<Complaint>())).Returns(new ComplaintResponse());
        var markets = new Mock<INightMarketRepository>();
        markets.Setup(value => value.GetActiveByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NightMarket?)null);
        var service = new ComplaintService(
            new ComplaintRepository(context), new BoothRepository(context), new OrderRepository(context),
            markets.Object, Mock.Of<ISubscriptionRepository>(), Mock.Of<IModerationRepository>(), mapper.Object,
            PersistentNotificationService(context));

        var created = await service.CreateAsync(seed.CustomerA, new CreateComplaintRequest
        {
            OrderId = seed.OrderComplaintService,
            BoothId = Guid.Empty,
            Title = "Missing item",
            Description = "One ordered item was missing from the package."
        });
        Assert.True(created.Success);
        var complaint = await context.Complaints.SingleAsync(value => value.OrderId == seed.OrderComplaintService);
        Assert.Equal(seed.CustomerA, complaint.CustomerId);
        Assert.Equal(seed.Booth, complaint.BoothId);
        Assert.Equal(ComplaintStatus.Pending, complaint.Status);
        Assert.Equal(paymentCount, await context.Payments.CountAsync());
        Assert.Equal(usageCount, await context.PromotionUsages.CountAsync());
        var orderAfter = await context.Orders.AsNoTracking().SingleAsync(value => value.Id == seed.OrderComplaintService);
        Assert.Equal((orderBefore.TotalAmount, orderBefore.DiscountAmount, orderBefore.FinalAmount, orderBefore.Status),
            (orderAfter.TotalAmount, orderAfter.DiscountAmount, orderAfter.FinalAmount, orderAfter.Status));

        var foreignOrder = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(seed.CustomerA,
            new CreateComplaintRequest
            {
                OrderId = seed.OrderForeign,
                BoothId = seed.Booth,
                Title = "Foreign order",
                Description = "This request must not access another customer's order."
            }));
        Assert.Equal(404, foreignOrder.StatusCode);
        var foreignComplaint = await Assert.ThrowsAsync<AppException>(() =>
            service.GetMineDetailAsync(seed.CustomerB, complaint.Id));
        Assert.Equal(404, foreignComplaint.StatusCode);
    }

    private static async Task AssertHistoryPaymentRefundAndIdorAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        await using var context = new SNMDbContext(options);
        var orders = new OrderRepository(context);
        var detail = await orders.GetCustomerDetailAsync(seed.CustomerA, seed.OrderHistory);
        Assert.NotNull(detail);
        Assert.Equal("Historical food", Assert.Single(detail!.Items).FoodName);
        Assert.Equal(42_000m, detail.Items.Single().UnitPrice);
        Assert.Equal([PaymentStatus.RefundProcessing, PaymentStatus.Paid], detail.Payments.Select(value => value.Status));
        Assert.Equal(42_000m, detail.Payments.First().RefundAmount);
        Assert.Null(await orders.GetCustomerDetailAsync(seed.CustomerB, seed.OrderHistory));

        var firstPage = await orders.GetCustomerHistoryAsync(seed.CustomerA, null, 1, 2);
        var secondPage = await orders.GetCustomerHistoryAsync(seed.CustomerA, null, 2, 2);
        Assert.Empty(firstPage.Items.Select(value => value.OrderId).Intersect(secondPage.Items.Select(value => value.OrderId)));

        Assert.DoesNotContain(typeof(CustomerOrderPaymentResponse).GetProperties(), property =>
            property.Name.Contains("Bank", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Signature", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("GatewayRef", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task AssertStablePaginationAsync(DbContextOptions<SNMDbContext> options, SeedIds seed)
    {
        var sameTimestamp = DateTime.UtcNow.AddDays(-10);
        await using var context = new SNMDbContext(options);
        await context.Orders.Where(value => value.Id == seed.OrderReviewA || value.Id == seed.OrderReviewB)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.CreatedAt, sameTimestamp));
        await context.Reviews.Where(value => value.OrderId == seed.OrderReviewA || value.OrderId == seed.OrderReviewB)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.CreatedAt, sameTimestamp));
        await context.Complaints.Where(value => value.OrderId == seed.OrderComplaintConcurrent)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.CreatedAt, sameTimestamp));

        var orders = new OrderRepository(context);
        var orderPage1 = await orders.GetCustomerHistoryAsync(seed.CustomerA, null, 1, 1);
        var orderPage2 = await orders.GetCustomerHistoryAsync(seed.CustomerA, null, 2, 1);
        var orderPage1Repeat = await orders.GetCustomerHistoryAsync(seed.CustomerA, null, 1, 1);
        Assert.Equal(orderPage1.Items.Single().OrderId, orderPage1Repeat.Items.Single().OrderId);
        Assert.NotEqual(orderPage1.Items.Single().OrderId, orderPage2.Items.Single().OrderId);

        var reviews = new ReviewRepository(context);
        var reviewPage1 = await reviews.GetPagedByCustomerWithReplyAsync(seed.CustomerA, 1, 1);
        var reviewPage2 = await reviews.GetPagedByCustomerWithReplyAsync(seed.CustomerA, 2, 1);
        var reviewPage1Repeat = await reviews.GetPagedByCustomerWithReplyAsync(seed.CustomerA, 1, 1);
        Assert.Equal(reviewPage1.Items.Single().Id, reviewPage1Repeat.Items.Single().Id);
        Assert.NotEqual(reviewPage1.Items.Single().Id, reviewPage2.Items.Single().Id);

        var complaints = new ComplaintRepository(context);
        var complaintPage1 = await complaints.GetPagedByCustomerWithImagesAsync(seed.CustomerA, 1, 1);
        var complaintPage2 = await complaints.GetPagedByCustomerWithImagesAsync(seed.CustomerA, 2, 1);
        var complaintPage1Repeat = await complaints.GetPagedByCustomerWithImagesAsync(seed.CustomerA, 1, 1);
        Assert.Equal(complaintPage1.Items.Single().Id, complaintPage1Repeat.Items.Single().Id);
        Assert.NotEqual(complaintPage1.Items.Single().Id, complaintPage2.Items.Single().Id);
    }

    private static NotificationService PersistentNotificationService(SNMDbContext context)
    {
        var mapper = new Mock<IMapper>();
        mapper.Setup(value => value.Map<NotificationListItemResponse>(It.IsAny<Notification>()))
            .Returns(new NotificationListItemResponse());
        var realtime = new Mock<IRealtimeNotificationPublisher>();
        realtime.Setup(value => value.PublishAsync(It.IsAny<Guid>(), It.IsAny<NotificationListItemResponse>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var presence = new Mock<IOnlinePresenceService>();
        presence.Setup(value => value.IsOnline(It.IsAny<Guid>())).Returns(true);
        var users = new Mock<IUserRepository>();
        users.Setup(value => value.GetActiveRecipientIdsAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        return new NotificationService(
            new NotificationRepository(context), Mock.Of<IUserDeviceTokenRepository>(), users.Object,
            Mock.Of<IPushNotificationService>(), realtime.Object, presence.Object, mapper.Object,
            NullLogger<NotificationService>.Instance);
    }

    private static Review NewReview(Guid id, Guid customerId, Guid orderId, Guid boothId, short rating) => new()
    {
        Id = id, CustomerId = customerId, OrderId = orderId, BoothId = boothId, Rating = rating,
        IsVisible = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Complaint NewComplaint(Guid customerId, Guid orderId, Guid boothId) => new()
    {
        Id = Guid.NewGuid(), CustomerId = customerId, OrderId = orderId, BoothId = boothId,
        Title = "Concurrent complaint", Description = "Concurrent duplicate verification.",
        Status = ComplaintStatus.Pending, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static async Task AssertRatingRejectedAsync(string connectionString, SeedIds seed, Guid orderId, short rating)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Reviews"
                ("Id", "BoothId", "CustomerId", "OrderId", "Rating", "IsVisible", "CreatedAt", "UpdatedAt")
            VALUES (@id, @booth, @customer, @order, @rating, true, now(), now())
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("booth", seed.Booth);
        command.Parameters.AddWithValue("customer", seed.CustomerA);
        command.Parameters.AddWithValue("order", orderId);
        command.Parameters.AddWithValue("rating", rating);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_reviews_rating", exception.ConstraintName);
    }

    private static async Task<SeedIds> SeedAsync(string connectionString)
    {
        var seed = new SeedIds(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "NightMarket"
                ("Id", "Name", "Address", "OpeningHours", "ClosingHours", "Status", "ModerationStatus", "IsDeleted", "TotalBooth", "CreatedAt", "UpdatedAt")
            VALUES (@market, 'Verification market', 'Test', '00:00', '23:59:59', 'Open', 'Active', false, 1, now(), now());
            INSERT INTO "User"
                ("Id", "RoleId", "UserName", "PasswordHash", "FullName", "Email", "AuthProvider", "Status", "CreatedAt", "UpdatedAt")
            VALUES
                (@owner, @role, 'verify-owner', 'hash', 'Owner', 'verify-owner@test.local', 'Local', 'Active', now(), now()),
                (@foreignOwner, @role, 'verify-owner-b', 'hash', 'Owner B', 'verify-owner-b@test.local', 'Local', 'Active', now(), now()),
                (@customerA, @role, 'verify-a', 'hash', 'Customer A', 'verify-a@test.local', 'Local', 'Active', now(), now()),
                (@customerB, @role, 'verify-b', 'hash', 'Customer B', 'verify-b@test.local', 'Local', 'Active', now(), now());
            INSERT INTO "Booth"
                ("Id", "RegistrationId", "NightMarketId", "BoothOwnerId", "BoothName", "Status", "CreatedAt", "UpdatedAt")
            VALUES (@booth, @registration, @market, @owner, 'Verification booth', 'Active', now(), now());
            INSERT INTO "FoodCategories" ("Id", "BoothId", "Name", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (@category, @booth, 'Category', false, now(), now());
            INSERT INTO "FoodItem"
                ("Id", "BoothId", "CategoryId", "Name", "Price", "IsAvailable", "IsFeatured", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (@food, @booth, @category, 'Current renamed food', 999000, true, false, false, now(), now());
            """, connection);
        command.Parameters.AddWithValue("market", seed.Market);
        command.Parameters.AddWithValue("owner", seed.Owner);
        command.Parameters.AddWithValue("foreignOwner", seed.ForeignOwner);
        command.Parameters.AddWithValue("customerA", seed.CustomerA);
        command.Parameters.AddWithValue("customerB", seed.CustomerB);
        command.Parameters.AddWithValue("role", Guid.NewGuid());
        command.Parameters.AddWithValue("booth", seed.Booth);
        command.Parameters.AddWithValue("registration", Guid.NewGuid());
        command.Parameters.AddWithValue("category", seed.Category);
        command.Parameters.AddWithValue("food", seed.Food);
        await command.ExecuteNonQueryAsync();

        var orderIds = new[]
        {
            seed.OrderReviewA, seed.OrderReviewB, seed.OrderInvalidLow, seed.OrderInvalidHigh,
            seed.OrderComplaintConcurrent, seed.OrderComplaintService, seed.OrderForeign, seed.OrderHistory
        };
        for (var index = 0; index < orderIds.Length; index++)
        {
            var customer = orderIds[index] == seed.OrderForeign ? seed.CustomerB : seed.CustomerA;
            await using var order = new NpgsqlCommand(
                """
                INSERT INTO "Order"
                    ("Id", "CustomerId", "BoothOwnerId", "OrderCode", "Status", "TotalAmount", "DiscountAmount", "FinalAmount", "CreatedAt", "UpdatedAt")
                VALUES (@id, @customer, @owner, @code, 'Completed', 42000, 0, 42000, @created, @created);
                INSERT INTO "OrderDetail"
                    ("Id", "OrderId", "FoodItemId", "FoodNameSnapshot", "Quantity", "UnitPrice", "TotalPrice", "CreatedAt", "UpdatedAt")
                VALUES (@detail, @id, @food, 'Historical food', 1, 42000, 42000, @created, @created);
                """, connection);
            order.Parameters.AddWithValue("id", orderIds[index]);
            order.Parameters.AddWithValue("customer", customer);
            order.Parameters.AddWithValue("owner", seed.Owner);
            order.Parameters.AddWithValue("code", 800_000_000_000_000L + index);
            order.Parameters.AddWithValue("detail", Guid.NewGuid());
            order.Parameters.AddWithValue("food", seed.Food);
            order.Parameters.AddWithValue("created", DateTime.UtcNow.AddMinutes(-index));
            await order.ExecuteNonQueryAsync();
        }

        await using var payments = new NpgsqlCommand(
            """
            INSERT INTO "Payments"
                ("Id", "OrderId", "BoothOwnerId", "Type", "Gateway", "Amount", "Status", "PaidAt", "CreatedAt", "UpdatedAt")
            VALUES (@paid, @order, @owner, 'PayOS', 'Payos', 42000, 'Paid', now() - interval '2 minutes', now() - interval '2 minutes', now());
            INSERT INTO "Payments"
                ("Id", "OrderId", "BoothOwnerId", "Type", "Gateway", "Amount", "Status", "RefundAmount", "RefundRequestedAt", "CreatedAt", "UpdatedAt")
            VALUES (@refund, @order, @owner, 'PayOS', 'Payos', 42000, 'RefundProcessing', 42000, now(), now(), now());
            """, connection);
        payments.Parameters.AddWithValue("paid", Guid.NewGuid());
        payments.Parameters.AddWithValue("refund", Guid.NewGuid());
        payments.Parameters.AddWithValue("order", seed.OrderHistory);
        payments.Parameters.AddWithValue("owner", seed.Owner);
        await payments.ExecuteNonQueryAsync();
        return seed;
    }

    private static async Task<LegacyIds> SeedLegacySnapshotsAsync(string connectionString)
    {
        var ids = new LegacyIds(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO "FoodItem"
                ("Id", "BoothId", "CategoryId", "Name", "Price", "IsAvailable", "IsFeatured", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (@food, @booth, @category, 'Legacy current food name', 50000, true, false, false, now(), now());
            INSERT INTO "Promotion"
                ("Id", "BoothId", "PromotionCode", "Title", "DiscountType", "Scope", "DiscountValue",
                 "IsPublic", "StartDate", "EndDate", "Status", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES (@promotion, @booth, 'LEGACY', 'Legacy current promotion title', 'FixedAmount', 'EntireBoothOrder', 5000,
                    true, now() - interval '1 day', now() + interval '1 day', 'Active', false, now(), now());
            INSERT INTO "Order"
                ("Id", "CustomerId", "BoothOwnerId", "OrderCode", "Status", "TotalAmount", "DiscountAmount", "FinalAmount", "CreatedAt", "UpdatedAt")
            VALUES (@order, @customer, @owner, 799999999999999, 'Completed', 50000, 5000, 45000, now(), now());
            INSERT INTO "OrderDetail"
                ("Id", "OrderId", "FoodItemId", "Quantity", "UnitPrice", "TotalPrice", "CreatedAt", "UpdatedAt")
            VALUES (@detail, @order, @food, 1, 50000, 50000, now(), now());
            INSERT INTO "PromotionUsages"
                ("Id", "PromotionId", "OrderId", "CustomerId", "DiscountAmount", "Status", "AppliedAt", "CreatedAt", "UpdatedAt")
            VALUES (@usage, @promotion, @order, @customer, 5000, 'Consumed', now(), now(), now());
            """, connection);
        command.Parameters.AddWithValue("food", ids.Food);
        command.Parameters.AddWithValue("booth", Guid.NewGuid());
        command.Parameters.AddWithValue("category", Guid.NewGuid());
        command.Parameters.AddWithValue("promotion", ids.Promotion);
        command.Parameters.AddWithValue("order", ids.Order);
        command.Parameters.AddWithValue("customer", Guid.NewGuid());
        command.Parameters.AddWithValue("owner", Guid.NewGuid());
        command.Parameters.AddWithValue("detail", ids.OrderDetail);
        command.Parameters.AddWithValue("usage", ids.PromotionUsage);
        await command.ExecuteNonQueryAsync();
        return ids;
    }

    private static async Task AssertLegacyBackfillAsync(string connectionString, LegacyIds ids)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var food = new NpgsqlCommand(
            "SELECT \"FoodNameSnapshot\" FROM \"OrderDetail\" WHERE \"Id\" = @id", connection))
        {
            food.Parameters.AddWithValue("id", ids.OrderDetail);
            Assert.Equal("Legacy current food name", await food.ExecuteScalarAsync());
        }
        await using (var promotion = new NpgsqlCommand(
            "SELECT \"PromotionCodeSnapshot\", \"PromotionTitleSnapshot\" FROM \"PromotionUsages\" WHERE \"Id\" = @id", connection))
        {
            promotion.Parameters.AddWithValue("id", ids.PromotionUsage);
            await using var reader = await promotion.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("LEGACY", reader.GetString(0));
            Assert.Equal("Legacy current promotion title", reader.GetString(1));
        }
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarStringAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (string?)await command.ExecuteScalarAsync() ?? string.Empty;
    }

    private sealed record SeedIds(
        Guid Market, Guid Owner, Guid ForeignOwner, Guid CustomerA, Guid CustomerB, Guid Booth, Guid Category, Guid Food,
        Guid OrderReviewA, Guid OrderReviewB, Guid OrderInvalidLow, Guid OrderInvalidHigh,
        Guid OrderComplaintConcurrent, Guid OrderComplaintService)
    {
        public Guid OrderForeign { get; } = Guid.NewGuid();
        public Guid OrderHistory { get; } = Guid.NewGuid();
    }

    private sealed record LegacyIds(Guid Food, Guid Promotion, Guid Order, Guid OrderDetail, Guid PromotionUsage);
}
