using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Danh sách vai trò người dùng trong hệ thống (Customer, BoothOwner, Admin)
/// </summary>
public partial class Role
{
    public Guid Id { get; set; }

    public string RoleName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
