using ApplicationLayer.AI.DTOs;
using System.ComponentModel.DataAnnotations;

namespace TestingLayer;

public class AIRequestValidationTests
{
    [Theory]
    [InlineData("FullMeal")]
    [InlineData("LightMeal")]
    [InlineData("FoodTour")]
    [InlineData("DateNight")]
    [InlineData("Family")]
    public void DiningStyle_DocumentedValue_IsValid(string diningStyle)
    {
        var request = new DiningPlanAssistantRequest
        {
            GroupSize = 1,
            Budget = 1,
            DiningStyle = diningStyle
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void DiningStyle_UndocumentedValue_IsRejected()
    {
        var request = new DiningPlanAssistantRequest
        {
            GroupSize = 1,
            Budget = 1,
            DiningStyle = "Banquet"
        };

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.DiningStyle)));
    }

    [Theory]
    [InlineData("BestMatch")]
    [InlineData("BudgetFriendly")]
    [InlineData("HighRating")]
    public void RegeneratePriority_DocumentedValue_IsValid(string priority)
    {
        Assert.Empty(Validate(new RegenerateDiningPlanRequest { LogId = Guid.NewGuid(), Priority = priority }));
    }

    [Fact]
    public void RegeneratePriority_UndocumentedValue_IsRejected()
    {
        var request = new RegenerateDiningPlanRequest { LogId = Guid.NewGuid(), Priority = "CheapestEver" };

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.Priority)));
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
