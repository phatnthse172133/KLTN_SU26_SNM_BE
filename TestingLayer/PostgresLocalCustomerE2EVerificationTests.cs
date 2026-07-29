using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using Microsoft.EntityFrameworkCore;

namespace TestingLayer;

public sealed class PostgresLocalCustomerE2EVerificationTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task AppliedLocalDatabase_ContainsSeedAndVerifiedCustomerMutationFlow()
    {
        var connectionString = Environment.GetEnvironmentVariable("SNM_APPLIED_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new SNMDbContext(options);

        Assert.Equal(18, await db.FoodCategories.CountAsync(category => category.IsSystem));
        Assert.Equal(102, await db.FoodTags.CountAsync(tag => tag.IsSystem && !tag.IsDeleted));
        Assert.True(await db.FoodItemTags.CountAsync(link =>
            link.FoodItem.Booth.NightMarketId == IntegrationDemoDataSeeder.MarketId) >= 60);
        foreach (var courseCode in new[]
                 {
                     "COURSE_APPETIZER", "COURSE_MAIN_COURSE", "COURSE_DRINK", "COURSE_DESSERT"
                 })
        {
            Assert.True(await db.FoodItemTags.AnyAsync(link =>
                link.FoodItem.Booth.NightMarketId == IntegrationDemoDataSeeder.MarketId
                && link.FoodTag.Code == courseCode), $"Missing applied course mapping {courseCode}.");
        }

        var customerId = Guid.Parse("d3500000-0000-0000-0000-000000000010");
        var customer = await db.Users.AsNoTracking().SingleAsync(user => user.Id == customerId);
        Assert.Equal("Khách Demo An", customer.FullName);
        Assert.Equal("0900000350", customer.Phone);

        var smokeOrders = await db.Orders.AsNoTracking()
            .Where(order => order.CustomerId == customerId && order.Note == "Local E2E smoke")
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync();
        Assert.NotEmpty(smokeOrders);
        Assert.Equal(smokeOrders.Count, smokeOrders.Select(value => value.CheckoutRequestId).Distinct().Count());
        var order = smokeOrders[0];
        Assert.NotEqual(Guid.Empty, order.CheckoutRequestId);
        Assert.True(await db.OrderDetails.AnyAsync(detail => detail.OrderId == order.Id));
        Assert.True(await db.Payments.AnyAsync(payment => payment.OrderId == order.Id));
    }
}
