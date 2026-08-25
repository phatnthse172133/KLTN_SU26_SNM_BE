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
        reviews.Setup(repository => repository.GetWithReplyByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Review
            {
                Id = id,
                OrderId = orderId,
                CustomerId = customerId,
                BoothId = boothId,
                Rating = 5,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
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
        reviews.Setup(repository => repository.GetWithReplyByIdAsync(reviewId))
            .ReturnsAsync(new Review { Id = reviewId, CustomerId = Guid.NewGuid(), Rating = 3, CreatedAt = DateTime.UtcNow });
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
    public async Task ReviewCreate_RejectsNonCompletedOrder()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new Mock<IOrderRepository>();
        orders.Setup(repository => repository.GetByCustomerAsync(customerId, orderId))
            .ReturnsAsync(new Order { Id = orderId, CustomerId = customerId, Status = OrderStatus.Preparing });
        var service = CreateReviewService(orders: orders);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(
            customerId, new CreateReviewRequest { OrderId = orderId, Rating = 5 }));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task ComplaintCreate_RejectsForeignOrder()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new Mock<IOrderRepository>();
        orders.Setup(repository => repository.GetByCustomerAsync(customerId, orderId))
            .ReturnsAsync((Order?)null);
        var service = new ComplaintService(
            new Mock<IComplaintRepository>().Object,
            new Mock<IBoothRepository>().Object,
            orders.Object,
            new Mock<INightMarketRepository>().Object,
            new Mock<ISubscriptionRepository>().Object,
            new Mock<IModerationRepository>().Object,
            new Mock<IMapper>().Object,
            new Mock<INotificationService>().Object,
            new Mock<ApplicationLayer.Services.Storage.IFileStorageService>().Object);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(customerId,
            new CreateComplaintRequest
            {
                OrderId = orderId,
                Category = ComplaintCategory.Other,
                Description = "Foreign order complaint body."
            }));

        Assert.Equal(404, exception.StatusCode);
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

        var reviews = new Mock<IReviewRepository>();
        reviews.Setup(repository => repository.ExistsByOrderAsync(orderId)).ReturnsAsync(true);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ReviewSettings:EditWindowDays"] = "7" })
            .Build();
        var service = new ReviewService(
            reviews.Object,
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

    [Fact]
    public async Task ReviewCreate_RejectsForeignOrder()
    {
        var orders = new Mock<IOrderRepository>();
        orders.Setup(repository => repository.GetByCustomerAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order?)null);
        var service = CreateReviewService(orders: orders);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(
            Guid.NewGuid(), new CreateReviewRequest { OrderId = Guid.NewGuid(), Rating = 5 }));

        Assert.Equal(404, exception.StatusCode);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)5)]
    public async Task ReviewCreate_AcceptsBoundaryRatings(short rating)
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
        reviews.Setup(repository => repository.ExistsByOrderAsync(orderId)).ReturnsAsync(false);
        reviews.Setup(repository => repository.TrySaveNewReviewAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reviews.Setup(repository => repository.GetWithReplyByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Review
            {
                Id = id,
                OrderId = orderId,
                CustomerId = customerId,
                BoothId = boothId,
                Rating = rating,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        mapper.Setup(instance => instance.Map<Review>(It.IsAny<CreateReviewRequest>()))
            .Returns((CreateReviewRequest request) => new Review { OrderId = request.OrderId, Rating = request.Rating });
        var service = CreateReviewService(reviews, orders, mapper);

        var result = await service.CreateAsync(customerId, new CreateReviewRequest { OrderId = orderId, Rating = rating });

        Assert.True(result.Success);
    }

    [Fact]
    public async Task FoodReviewCreate_RejectsOrderDetailNotOwned()
    {
        var orders = new Mock<IOrderRepository>();
        orders.Setup(repository => repository.GetCustomerOrderDetailLineAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetail?)null);
        var service = CreateReviewService(orders: orders);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateFoodReviewAsync(
            Guid.NewGuid(), new CreateFoodReviewRequest { OrderDetailId = Guid.NewGuid(), Rating = 4 }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("ORDER_DETAIL_NOT_FOUND", exception.ErrorCode);
    }

    [Fact]
    public async Task FoodReviewCreate_RejectsWhenBoothReviewMissing()
    {
        var customerId = Guid.NewGuid();
        var orderDetailId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new Mock<IOrderRepository>();
        var reviews = new Mock<IReviewRepository>();
        orders.Setup(repository => repository.GetCustomerOrderDetailLineAsync(customerId, orderDetailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetail
            {
                Id = orderDetailId,
                OrderId = orderId,
                FoodItemId = Guid.NewGuid(),
                Order = new Order { Id = orderId, CustomerId = customerId, Status = OrderStatus.Completed },
                FoodItem = new FoodItem { Id = Guid.NewGuid(), BoothId = Guid.NewGuid(), Name = "Pho" }
            });
        reviews.Setup(repository => repository.ExistsByOrderAsync(orderId)).ReturnsAsync(false);
        var service = CreateReviewService(reviews: reviews, orders: orders);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateFoodReviewAsync(
            customerId, new CreateFoodReviewRequest { OrderDetailId = orderDetailId, Rating = 5 }));

        Assert.Equal("ORDER_REVIEW_REQUIRED", exception.ErrorCode);
    }

    [Fact]
    public async Task ComplaintCreate_RejectsInvalidCategory()
    {
        var service = CreateComplaintService(new Mock<IComplaintRepository>());
        var exception = await Assert.ThrowsAsync<AppException>(() => service.CreateAsync(
            Guid.NewGuid(),
            new CreateComplaintRequest
            {
                OrderId = Guid.NewGuid(),
                Category = (ComplaintCategory)99,
                Description = "Invalid category body."
            }));
        Assert.Equal("COMPLAINT_CATEGORY_INVALID", exception.ErrorCode);
    }

    [Fact]
    public async Task ComplaintCreate_DoesNotRequireCompletedOrder()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var complaints = new Mock<IComplaintRepository>();
        var orders = new Mock<IOrderRepository>();
        var booths = new Mock<IBoothRepository>();
        var mapper = new Mock<IMapper>();
        orders.Setup(value => value.GetByCustomerAsync(customerId, orderId))
            .ReturnsAsync(new Order { Id = orderId, CustomerId = customerId, Status = OrderStatus.Preparing });
        orders.Setup(value => value.GetBoothIdForCustomerOrderAsync(customerId, orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(boothId);
        complaints.Setup(value => value.HasActiveComplaintAsync(customerId, boothId, orderId)).ReturnsAsync(false);
        complaints.Setup(value => value.TrySaveNewComplaintAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        complaints.Setup(value => value.GetWithImagesByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Complaint
            {
                Id = id,
                CustomerId = customerId,
                BoothId = boothId,
                OrderId = orderId,
                Category = ComplaintCategory.DelayedOrder,
                Title = "Delayed",
                Description = "Order is delayed too long.",
                Status = ComplaintStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        mapper.Setup(value => value.Map<Complaint>(It.IsAny<CreateComplaintRequest>()))
            .Returns(new Complaint());
        mapper.Setup(value => value.Map<ComplaintResponse>(It.IsAny<Complaint>()))
            .Returns((Complaint complaint) => new ComplaintResponse
            {
                Id = complaint.Id,
                Status = complaint.Status.ToString(),
                Category = complaint.Category.ToString()
            });
        booths.Setup(value => value.GetByIdAsync(boothId)).ReturnsAsync((Booth?)null);

        var service = new ComplaintService(
            complaints.Object, booths.Object, orders.Object,
            new Mock<INightMarketRepository>().Object, new Mock<ISubscriptionRepository>().Object,
            new Mock<IModerationRepository>().Object, mapper.Object, new Mock<INotificationService>().Object,
            new Mock<ApplicationLayer.Services.Storage.IFileStorageService>().Object);

        var result = await service.CreateAsync(customerId, new CreateComplaintRequest
        {
            OrderId = orderId,
            Category = ComplaintCategory.DelayedOrder,
            Description = "Order is delayed too long."
        });

        Assert.True(result.Success);
        Assert.True(result.Data!.CanWithdraw);
        Assert.False(result.Data.CanAddEvidence);
    }

    [Fact]
    public async Task ComplaintWithdraw_IsTerminalForActiveRule()
    {
        var customerId = Guid.NewGuid();
        var complaintId = Guid.NewGuid();
        var complaints = new Mock<IComplaintRepository>();
        complaints.Setup(value => value.GetCustomerWithImagesByIdAsync(customerId, complaintId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Complaint
            {
                Id = complaintId,
                CustomerId = customerId,
                Status = ComplaintStatus.Pending,
                Title = "Title",
                Description = "Description body"
            });
        complaints.Setup(value => value.UpdateStatusWithConcurrencyAsync(
                complaintId, ComplaintStatus.Pending, ComplaintStatus.Withdrawn,
                It.IsAny<string?>(), It.IsAny<ComplaintResolutionAction?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(1);
        complaints.Setup(value => value.GetWithImagesByIdAsync(complaintId))
            .ReturnsAsync(new Complaint
            {
                Id = complaintId,
                CustomerId = customerId,
                Status = ComplaintStatus.Withdrawn,
                Title = "Title",
                Description = "Description body"
            });
        var mapper = new Mock<IMapper>();
        mapper.Setup(value => value.Map<ComplaintResponse>(It.IsAny<Complaint>()))
            .Returns((Complaint complaint) => new ComplaintResponse { Id = complaint.Id, Status = complaint.Status.ToString() });

        var service = new ComplaintService(
            complaints.Object,
            new Mock<IBoothRepository>().Object,
            new Mock<IOrderRepository>().Object,
            new Mock<INightMarketRepository>().Object,
            new Mock<ISubscriptionRepository>().Object,
            new Mock<IModerationRepository>().Object,
            mapper.Object,
            new Mock<INotificationService>().Object,
            new Mock<ApplicationLayer.Services.Storage.IFileStorageService>().Object);

        var result = await service.WithdrawAsync(customerId, complaintId);
        Assert.True(result.Success);
        Assert.Equal("Withdrawn", result.Data!.Status);
        Assert.False(result.Data.CanWithdraw);
    }

    private static ReviewService CreateReviewService(
        Mock<IReviewRepository>? reviews = null,
        Mock<IOrderRepository>? orders = null,
        Mock<IMapper>? mapper = null,
        Mock<IFoodReviewRepository>? foodReviews = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReviewSettings:EditWindowDays"] = "7"
            })
            .Build();

        var reviewRepo = reviews ?? new Mock<IReviewRepository>();
        var foodRepo = foodReviews ?? new Mock<IFoodReviewRepository>();
        foodRepo.Setup(repository => repository.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodReview>());
        foodRepo.Setup(repository => repository.GetByOrderIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, List<FoodReview>>());
        if (reviews is null)
        {
            reviewRepo.Setup(repository => repository.GetWithReplyByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => new Review
                {
                    Id = id,
                    OrderId = Guid.NewGuid(),
                    CustomerId = Guid.NewGuid(),
                    BoothId = Guid.NewGuid(),
                    Rating = 5,
                    IsVisible = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        return new ReviewService(
            reviewRepo.Object,
            new Mock<IBoothRepository>().Object,
            (orders ?? new Mock<IOrderRepository>()).Object,
            (mapper ?? new Mock<IMapper>()).Object,
            new Mock<INotificationService>().Object,
            new Mock<ISubscriptionEntitlementService>().Object,
            configuration,
            foodRepo.Object,
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
