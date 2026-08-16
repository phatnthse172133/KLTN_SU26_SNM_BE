using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

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
