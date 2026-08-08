using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
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
using Microsoft.Extensions.Configuration;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class CustomerHistoryReviewComplaintTests
{
    [Fact]
    public async Task CustomerOrderDetail_UsesSnapshotsAndHidesForeignOrder()
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new SNMDbContext(options);

        var customerId = Guid.NewGuid();
        var booth = new Booth
        {
            Id = Guid.NewGuid(), NightMarketId = Guid.NewGuid(),
            BoothOwnerId = Guid.NewGuid(), BoothName = "Original booth", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(), BoothId = booth.Id, CategoryId = Guid.NewGuid(), Name = "Renamed food",
            Price = 999_000m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var order = new Order
        {
            Id = Guid.NewGuid(), CustomerId = customerId, BoothOwnerId = booth.BoothOwnerId, OrderCode = 123456,
            Status = OrderStatus.Completed, TotalAmount = 80_000m, DiscountAmount = 10_000m, FinalAmount = 70_000m,
            CreatedAt = DateTime.UtcNow.AddHours(-1), UpdatedAt = DateTime.UtcNow
        };
        context.AddRange(booth, food, order);
        context.OrderDetails.Add(new OrderDetail
        {
            Id = Guid.NewGuid(), OrderId = order.Id, FoodItemId = food.Id, FoodNameSnapshot = "Original food",
            Quantity = 2, UnitPrice = 40_000m, TotalPrice = 80_000m, CreatedAt = order.CreatedAt, UpdatedAt = order.CreatedAt
        });
        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, BoothOwnerId = booth.BoothOwnerId,
            Type = PaymentType.PayOS, Gateway = PaymentGateway.Payos, Status = PaymentStatus.Paid,
            Amount = 70_000m, PaidAt = order.UpdatedAt, CreatedAt = order.CreatedAt, UpdatedAt = order.UpdatedAt
        });
        await context.SaveChangesAsync();

        var repository = new OrderRepository(context);
        var detail = await repository.GetCustomerDetailAsync(customerId, order.Id);

        Assert.NotNull(detail);
        Assert.Equal("Original food", Assert.Single(detail!.Items).FoodName);
        Assert.NotEqual(Guid.Empty, detail.Items.Single().OrderDetailId);
        Assert.Equal(40_000m, detail.Items.Single().UnitPrice);
        Assert.Equal(70_000m, detail.FinalAmount);
        Assert.Equal(PaymentStatus.Paid, Assert.Single(detail.Payments).Status);
        Assert.Null(await repository.GetCustomerDetailAsync(Guid.NewGuid(), order.Id));
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)6)]
    public async Task ReviewCreate_RejectsRatingOutsideBusinessRange(short rating)
    {
        var service = CreateReviewService();
        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(
            Guid.NewGuid(), new CreateReviewRequest { OrderId = Guid.NewGuid(), Rating = rating }));
        Assert.Equal("INVALID_REVIEW_RATING", exception.ErrorCode);
    }

    [Fact]
    public async Task ReviewCreate_DerivesBoothFromCompletedOwnedOrder()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var reviews = new Mock<IReviewRepository>();
        var orders = new Mock<IOrderRepository>();
        var mapper = new Mock<IMapper>();
        orders.Setup(repository => repository.GetByCustomerAsync(customerId, orderId))
            .ReturnsAsync(new Order { Id = orderId, CustomerId = customerId, Status = OrderStatus.Completed });
        orders.Setup(repository => repository.GetBoothIdForCustomerOrderAsync(customerId, orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(boothId);
        reviews.Setup(repository => repository.TrySaveNewReviewAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mapper.Setup(instance => instance.Map<Review>(It.IsAny<CreateReviewRequest>()))
            .Returns((CreateReviewRequest request) => new Review { OrderId = request.OrderId, Rating = request.Rating });
        mapper.Setup(instance => instance.Map<ReviewResponse>(It.IsAny<Review>())).Returns(new ReviewResponse());
        var service = CreateReviewService(reviews, orders, mapper);

        await service.CreateAsync(customerId, new CreateReviewRequest { OrderId = orderId, Rating = 5 });

        reviews.Verify(repository => repository.AddAsync(It.Is<Review>(review =>
            review.CustomerId == customerId && review.BoothId == boothId && review.OrderId == orderId)), Times.Once);
    }

    [Fact]
    public async Task ReviewUpdate_HidesForeignReviewAsNotFound()
    {
        var reviews = new Mock<IReviewRepository>();
        var reviewId = Guid.NewGuid();
        reviews.Setup(repository => repository.GetByIdAsync(reviewId))
            .ReturnsAsync(new Review { Id = reviewId, CustomerId = Guid.NewGuid(), Rating = 3 });
        var service = CreateReviewService(reviews: reviews);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.UpdateAsync(
            Guid.NewGuid(), reviewId, new UpdateReviewRequest { Rating = 4 }));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task ReviewCreate_DatabaseRaceReturnsStableConflict()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var reviews = new Mock<IReviewRepository>();
        var orders = new Mock<IOrderRepository>();
        var mapper = new Mock<IMapper>();
        orders.Setup(value => value.GetByCustomerAsync(customerId, orderId))
            .ReturnsAsync(new Order { Id = orderId, CustomerId = customerId, Status = OrderStatus.Completed });
        orders.Setup(value => value.GetBoothIdForCustomerOrderAsync(customerId, orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(boothId);
        mapper.Setup(value => value.Map<Review>(It.IsAny<CreateReviewRequest>()))
            .Returns(new Review { OrderId = orderId, Rating = 5 });
        reviews.Setup(value => value.TrySaveNewReviewAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = CreateReviewService(reviews, orders, mapper);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(
            customerId, new CreateReviewRequest { OrderId = orderId, Rating = 5 }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("REVIEW_ALREADY_EXISTS", exception.ErrorCode);
    }

    [Fact]
    public async Task ComplaintCreate_DatabaseRaceReturnsStableConflict()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var complaints = new Mock<IComplaintRepository>();
        var orders = new Mock<IOrderRepository>();
        var mapper = new Mock<IMapper>();
        orders.Setup(value => value.GetByCustomerAsync(customerId, orderId))
            .ReturnsAsync(new Order { Id = orderId, CustomerId = customerId });
        orders.Setup(value => value.GetBoothIdForCustomerOrderAsync(customerId, orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(boothId);
        mapper.Setup(value => value.Map<Complaint>(It.IsAny<CreateComplaintRequest>()))
            .Returns(new Complaint { OrderId = orderId, Title = "Duplicate", Description = "Duplicate complaint body." });
        complaints.Setup(value => value.TrySaveNewComplaintAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = new ComplaintService(
            complaints.Object, new Mock<IBoothRepository>().Object, orders.Object,
            new Mock<INightMarketRepository>().Object, new Mock<ISubscriptionRepository>().Object,
            new Mock<IModerationRepository>().Object, mapper.Object, new Mock<INotificationService>().Object,
            new Mock<ApplicationLayer.Services.Storage.IFileStorageService>().Object);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(customerId,
            new CreateComplaintRequest
            {
                OrderId = orderId,
                Category = ComplaintCategory.Other,
                Title = "Duplicate",
                Description = "Duplicate complaint body."
            }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("ACTIVE_COMPLAINT_EXISTS", exception.ErrorCode);
    }

    [Fact]
    public async Task ComplaintDetail_HidesForeignComplaintAsNotFound()
    {
        var complaints = new Mock<IComplaintRepository>();
        complaints.Setup(repository => repository.GetCustomerWithImagesByIdAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Complaint?)null);
        var service = CreateComplaintService(complaints);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.GetMineDetailAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task ReviewUpdate_RejectsOutsideEditWindow()
    {
        var customerId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var reviews = new Mock<IReviewRepository>();
        reviews.Setup(repository => repository.GetWithReplyByIdAsync(reviewId))
            .ReturnsAsync(new Review
            {
                Id = reviewId,
                CustomerId = customerId,
                Rating = 3,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-10)
            });
        var service = CreateReviewService(reviews: reviews);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.UpdateAsync(
            customerId, reviewId, new UpdateReviewRequest { Rating = 4 }));

        Assert.Equal("REVIEW_EDIT_WINDOW_EXPIRED", exception.ErrorCode);
    }

    [Fact]
    public async Task FoodReviewCreate_RequiresCompletedOwnedOrderDetail()
    {
        var customerId = Guid.NewGuid();
        var orderDetailId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var foodItemId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var orders = new Mock<IOrderRepository>();
        var foodReviews = new Mock<IFoodReviewRepository>();
        orders.Setup(repository => repository.GetCustomerOrderDetailLineAsync(customerId, orderDetailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetail
            {
                Id = orderDetailId,
                OrderId = orderId,
                FoodItemId = foodItemId,
                Order = new Order { Id = orderId, CustomerId = customerId, Status = OrderStatus.Completed },
                FoodItem = new FoodItem { Id = foodItemId, BoothId = boothId, Name = "Bun" }
            });
        foodReviews.Setup(repository => repository.ExistsByOrderDetailAsync(orderDetailId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        foodReviews.Setup(repository => repository.TrySaveNewFoodReviewAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        foodReviews.Setup(repository => repository.GetByIdWithNavAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new FoodReview
            {
                Id = id,
                OrderDetailId = orderDetailId,
                OrderId = orderId,
                FoodItemId = foodItemId,
                CustomerId = customerId,
                BoothId = boothId,
                Rating = 5,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ReviewSettings:EditWindowDays"] = "7" })
            .Build();
        var service = new ReviewService(
            new Mock<IReviewRepository>().Object,
            new Mock<IBoothRepository>().Object,
            orders.Object,
            new Mock<IMapper>().Object,
            new Mock<INotificationService>().Object,
            new Mock<ISubscriptionEntitlementService>().Object,
            configuration,
            foodReviews.Object,
            new Mock<IFoodItemRepository>().Object,
            new Mock<ApplicationLayer.Services.Storage.IFileStorageService>().Object);

        var result = await service.CreateFoodReviewAsync(customerId, new CreateFoodReviewRequest
        {
            OrderDetailId = orderDetailId,
            Rating = 5
        });

        Assert.True(result.Success);
        foodReviews.Verify(repository => repository.AddAsync(It.Is<FoodReview>(review =>
            review.CustomerId == customerId &&
            review.OrderDetailId == orderDetailId &&
            review.BoothId == boothId)), Times.Once);
        foodReviews.Verify(repository => repository.RefreshFoodItemAverageRatingAsync(foodItemId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ReviewService CreateReviewService(
        Mock<IReviewRepository>? reviews = null,
        Mock<IOrderRepository>? orders = null,
        Mock<IMapper>? mapper = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReviewSettings:EditWindowDays"] = "7"
            })
            .Build();

        return new ReviewService(
            (reviews ?? new Mock<IReviewRepository>()).Object,
            new Mock<IBoothRepository>().Object,
            (orders ?? new Mock<IOrderRepository>()).Object,
            (mapper ?? new Mock<IMapper>()).Object,
            new Mock<INotificationService>().Object,
            new Mock<ISubscriptionEntitlementService>().Object,
            configuration,
            new Mock<IFoodReviewRepository>().Object,
            new Mock<IFoodItemRepository>().Object,
            new Mock<ApplicationLayer.Services.Storage.IFileStorageService>().Object);
    }

    private static ComplaintService CreateComplaintService(Mock<IComplaintRepository> complaints)
        => new(
            complaints.Object,
            new Mock<IBoothRepository>().Object,
            new Mock<IOrderRepository>().Object,
            new Mock<INightMarketRepository>().Object,
            new Mock<ISubscriptionRepository>().Object,
            new Mock<IModerationRepository>().Object,
            new Mock<IMapper>().Object,
            new Mock<INotificationService>().Object,
            new Mock<ApplicationLayer.Services.Storage.IFileStorageService>().Object);
}
