using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

// Tag chuẩn do Admin quản lý để mô tả ngữ nghĩa món ăn.
public partial class FoodTag : ISoftDelete
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string? Description { get; set; }

    public FoodTagGroup TagGroup { get; set; }

    public FoodTagStatus Status { get; set; }

    public bool IsSystem { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsSelectable { get; set; }

    public bool IsPreferenceSelectable { get; set; }

    public bool IsAutoAssigned { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<FoodItemTag> FoodItemTags { get; set; } = new List<FoodItemTag>();
}
