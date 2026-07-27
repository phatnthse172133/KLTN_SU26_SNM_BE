using DomainLayer.Entities;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class AICustomerContextRepositoryTests
{
    [Fact]
    public async Task Context_FeedbackIsBoundedAndIsolatedByCustomer()
    {
        await using var db = new SNMDbContext(new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var customerId = Guid.NewGuid();
        var positiveFoodId = Guid.NewGuid();
        var negativeFoodId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.AIRecommendationLogs.AddRange(
            Feedback(customerId, positiveFoodId, "Suitable", now),
            Feedback(customerId, negativeFoodId, "NotSuitable", now.AddMinutes(1)),
            Feedback(Guid.NewGuid(), positiveFoodId, "NotSuitable", now.AddMinutes(2)));
        await db.SaveChangesAsync();

        var context = await new AICustomerContextRepository(db).GetAsync(customerId, 20, 20);

        Assert.Equal(1, context.FoodFeedbackScores![positiveFoodId]);
        Assert.Equal(-1, context.FoodFeedbackScores[negativeFoodId]);
    }

    [Fact]
    public async Task AiCandidates_ExcludeCloserButHiddenMarket()
    {
        await using var db = new SNMDbContext(new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var tag = NewTag("MILD");
        var visibleFood = NewFood(tag);
        visibleFood.Booth.NightMarket.Status = NightMarketStatus.Open;
        visibleFood.Booth.NightMarket.ModerationStatus = ModerationStatus.Active;
        visibleFood.Booth.Status = BoothStatus.Active;
        var hiddenFood = NewFood(tag);
        hiddenFood.Booth.NightMarket.Status = NightMarketStatus.Open;
        hiddenFood.Booth.NightMarket.ModerationStatus = ModerationStatus.Suspended;
        hiddenFood.Booth.Status = BoothStatus.Active;
        db.FoodTags.Add(tag);
        db.FoodItems.AddRange(visibleFood, hiddenFood);
        await db.SaveChangesAsync();

        var candidates = await new FoodItemRepository(db).GetAiCandidatesAsync(null, 200);

        Assert.Contains(candidates, food => food.Id == visibleFood.Id);
        Assert.DoesNotContain(candidates, food => food.Id == hiddenFood.Id);
    }

    [Fact]
    public async Task Context_UsesOnlyRecentCompletedPaidOrdersAndEligibleReviews()
    {
        await using var db = new SNMDbContext(new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var customerId = Guid.NewGuid();
        var validTag = NewTag("VALID");
        var ignoredTag = NewTag("IGNORED");
        var validFood = NewFood(validTag);
        var ignoredFood = NewFood(ignoredTag);
        db.FoodTags.AddRange(validTag, ignoredTag);
        db.FoodItems.AddRange(validFood, ignoredFood);

        var oldValid = NewOrder(customerId, validFood, OrderStatus.Completed, PaymentStatus.Paid, 1, DateTime.UtcNow.AddDays(-2));
        var newestValid = NewOrder(customerId, validFood, OrderStatus.Completed, PaymentStatus.Paid, 3, DateTime.UtcNow);
        var cancelled = NewOrder(customerId, ignoredFood, OrderStatus.Cancelled, PaymentStatus.Paid, 20, DateTime.UtcNow.AddHours(-1));
        var failed = NewOrder(customerId, ignoredFood, OrderStatus.Completed, PaymentStatus.Failed, 30, DateTime.UtcNow.AddHours(-2));
        db.Orders.AddRange(oldValid, newestValid, cancelled, failed);
        db.Reviews.AddRange(
            NewReview(customerId, newestValid, validFood.BoothId, 5),
            NewReview(customerId, cancelled, ignoredFood.BoothId, 1));
        await db.SaveChangesAsync();

        var context = await new AICustomerContextRepository(db).GetAsync(customerId, 1, 30);

        Assert.Equal(3, context.TagQuantities[validTag.Id]);
        Assert.DoesNotContain(ignoredTag.Id, context.TagQuantities.Keys);
        Assert.Contains(validFood.BoothId, context.PositiveBoothIds);
        Assert.DoesNotContain(ignoredFood.BoothId, context.NegativeBoothIds);
        Assert.Equal(newestValid.OrderDetails.Single().UnitPrice, context.TypicalUnitPrice);
    }

    private static FoodTag NewTag(string code) => new()
    {
        Id = Guid.NewGuid(), Name = code, Code = code, Status = FoodTagStatus.Active
    };

    private static FoodItem NewFood(FoodTag tag)
    {
        var market = new NightMarket { Id = Guid.NewGuid(), Name = "Market", Address = "Address" };
        var booth = new Booth
        {
            Id = Guid.NewGuid(), BoothName = "Booth", NightMarketId = market.Id, NightMarket = market
        };
        var category = new FoodCategory { Id = Guid.NewGuid(), Name = "Category" };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(), BoothId = booth.Id, Booth = booth, CategoryId = category.Id,
            Category = category, Name = "Food", Price = 70_000m, IsAvailable = true
        };
        food.FoodItemTags.Add(new FoodItemTag
        {
            FoodItemId = food.Id, FoodItem = food, FoodTagId = tag.Id, FoodTag = tag
        });
        return food;
    }

    private static Order NewOrder(
        Guid customerId,
        FoodItem food,
        OrderStatus status,
        PaymentStatus paymentStatus,
        int quantity,
        DateTime createdAt)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(), CustomerId = customerId, BoothOwnerId = Guid.NewGuid(),
            OrderCode = Math.Abs(BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0)),
            Status = status, CreatedAt = createdAt, UpdatedAt = createdAt
        };
        order.OrderDetails.Add(new OrderDetail
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Order = order, FoodItemId = food.Id, FoodItem = food,
            FoodNameSnapshot = food.Name, Quantity = quantity, UnitPrice = food.Price,
            TotalPrice = food.Price * quantity, CreatedAt = createdAt
        });
        order.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Order = order, BoothOwnerId = order.BoothOwnerId,
            Status = paymentStatus, Amount = food.Price * quantity, CreatedAt = createdAt
        });
        return order;
    }

    private static Review NewReview(Guid customerId, Order order, Guid boothId, short rating) => new()
    {
        Id = Guid.NewGuid(), CustomerId = customerId, OrderId = order.Id, Order = order,
        BoothId = boothId, Rating = rating, CreatedAt = order.CreatedAt
    };

    private static AIRecommendationLog Feedback(Guid customerId, Guid foodItemId, string type, DateTime createdAt)
        => new()
        {
            Id = Guid.NewGuid(), CustomerId = customerId,
            RecommendationType = AIRecommendationType.PreferenceProfile,
            InputJson = System.Text.Json.JsonSerializer.Serialize(new { foodItemId, feedbackType = type }),
            ResultJson = "{}", CreatedAt = createdAt, UpdatedAt = createdAt
        };
}
