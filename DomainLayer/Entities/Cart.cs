using DomainLayer.Common;

namespace DomainLayer.Entities;

// Giỏ hàng hiện tại của khách hàng. Mỗi khách chỉ có tối đa một giỏ chưa bị xóa.
public class Cart : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
