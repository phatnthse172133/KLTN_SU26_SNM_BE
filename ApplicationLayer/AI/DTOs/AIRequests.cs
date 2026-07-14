using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.AI.DTOs;

public class FoodTagQueryRequest : ApplicationLayer.Helppers.PaginationReq
{
    public string? Search { get; set; }
    public FoodTagGroup? TagGroup { get; set; }
}

public class CreateFoodTagRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Code { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public FoodTagGroup TagGroup { get; set; }
}

public class UpdateFoodTagRequest : CreateFoodTagRequest
{
    public FoodTagStatus Status { get; set; } = FoodTagStatus.Active;
}

public class UpdateFoodItemTagsRequest
{
    public IReadOnlyCollection<Guid> TagIds { get; set; } = [];
}

public class UpdateCustomerPreferenceRequest
{
    public IReadOnlyCollection<Guid> LikedTagIds { get; set; } = [];
    public IReadOnlyCollection<Guid> AvoidTagIds { get; set; } = [];
}

public class FoodDiscoveryRequest
{
    [MaxLength(1000)]
    public string? Query { get; set; }

    public IReadOnlyCollection<Guid> SelectedTagIds { get; set; } = [];
    public decimal? BudgetMax { get; set; }
    public Guid? NightMarketId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool PreferNearMe { get; set; }
    public string SortBy { get; set; } = "BestMatch";
    public int Limit { get; set; } = 20;
}

public class DiningPlanAssistantRequest
{
    public Guid? NightMarketId { get; set; }

    [Range(1, 50)]
    public int GroupSize { get; set; } = 1;

    [Range(0, double.MaxValue)]
    public decimal Budget { get; set; }

    public string DiningStyle { get; set; } = "FullMeal";

    [MaxLength(1000)]
    public string? Query { get; set; }

    public IReadOnlyCollection<Guid> PreferredTagIds { get; set; } = [];
    public IReadOnlyCollection<Guid> AvoidTagIds { get; set; } = [];
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool PreferNearMe { get; set; }
}

public class ConfirmDiningPlanRequest
{
    public Guid LogId { get; set; }
    [Required]
    public string OptionId { get; set; } = null!;
}

public class RegenerateDiningPlanRequest
{
    public Guid LogId { get; set; }
    public string Priority { get; set; } = "BestMatch";
}

public class AIFeedbackRequest
{
    public Guid? LogId { get; set; }
    public string? OptionId { get; set; }
    public Guid? FoodItemId { get; set; }

    [Required, MaxLength(100)]
    public string FeedbackType { get; set; } = "NotSuitable";

    [MaxLength(500)]
    public string? Reason { get; set; }
}
