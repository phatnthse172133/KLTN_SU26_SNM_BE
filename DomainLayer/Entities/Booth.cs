using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Gian hàng ẩm thực - thực thể trung tâm, mỗi gian hàng thuộc 1 NightMarket và do 1 User (BoothOwner) quản lý
public partial class Booth
{
    public Guid Id { get; set; }

    public Guid NightMarketId { get; set; }

    public Guid BoothOwnerId { get; set; }

    public string BoothName { get; set; } = null!;

    public string? BoothNumber { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    public decimal? MapPositionX { get; set; }

    public decimal? MapPositionY { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public TimeOnly? OpenTime { get; set; }

    public TimeOnly? CloseTime { get; set; }

    // Cache điểm trung bình review, cập nhật qua trigger hoặc job định kỳ
    public decimal? AverageRating { get; set; }

    public bool IsFeatured { get; set; }

    public string? PackageName { get; set; }

    public DateTime? PackageExpiryDate { get; set; }

    // Pending: chờ Admin duyệt | Active: hoạt động | Inactive: tạm ngừng | Suspended: bị khóa do vi phạm
    public BoothStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BoothDocument> BoothDocuments { get; set; } = new List<BoothDocument>();

    public virtual ICollection<BoothImage> BoothImages { get; set; } = new List<BoothImage>();

    public virtual BoothLocation? BoothLocation { get; set; }

    public virtual User BoothOwner { get; set; } = null!;

    public virtual ICollection<BoothPaymentInfo> BoothPaymentInfos { get; set; } = new List<BoothPaymentInfo>();

    public virtual ICollection<BoothPromotionalPackage> BoothPromotionalPackages { get; set; } = new List<BoothPromotionalPackage>();

    public virtual BoothQrcode? BoothQrcode { get; set; }

    public virtual ICollection<BoothSubscription> BoothSubscriptions { get; set; } = new List<BoothSubscription>();

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();

    public virtual NightMarket NightMarket { get; set; } = null!;

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
