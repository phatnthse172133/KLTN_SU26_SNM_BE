using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

// Sở thích rõ ràng của khách hàng theo tag: thích hoặc tránh.
public partial class CustomerPreference
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid FoodTagId { get; set; }

    public CustomerPreferenceKind PreferenceKind { get; set; }

    public CustomerPreferenceSource PreferenceSource { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual FoodTag FoodTag { get; set; } = null!;
}
