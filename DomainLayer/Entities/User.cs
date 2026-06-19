using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Tài khoản người dùng - dùng chung cho Customer, BoothOwner, Admin (phân biệt qua RoleId)
public partial class User
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public string UserName { get; set; } = null!;

    // Mật khẩu đã được mã hóa (hash), tuyệt đối không lưu plaintext
    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public DateOnly? DoB { get; set; }

    public string? AvatarUrl { get; set; }

    // Active: đang hoạt động | Inactive: chưa xác thực | Banned: bị khóa bởi Admin
    public UserStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Booth> Booths { get; set; } = new List<Booth>();

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();

    public virtual ICollection<ReviewReply> ReviewReplies { get; set; } = new List<ReviewReply>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual Role Role { get; set; } = null!;
}
