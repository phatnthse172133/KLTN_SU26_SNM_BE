using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// TÃ i khoáº£n ngÆ°á»i dÃ¹ng - dÃ¹ng chung cho Customer, BoothOwner, Admin (phÃ¢n biá»‡t qua RoleId)
public partial class User
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public string UserName { get; set; } = null!;

    // Máº­t kháº©u Ä‘Ã£ Ä‘Æ°á»£c mÃ£ hÃ³a (hash), tuyá»‡t Ä‘á»‘i khÃ´ng lÆ°u plaintext
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Indicates that the account is using a system-generated temporary password.
    /// The user must set a personal password before using the account normally.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Market Owner who created this Booth Owner account through the invitation flow.
    /// Null for accounts created through public registration or other legacy flows.
    /// </summary>
    public Guid? CreatedByMarketOwnerId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public DateOnly? DoB { get; set; }

    public string? AvatarUrl { get; set; }

    // Local | Google | Local,Google
    public AuthProvider AuthProvider { get; set; }

    public string? GoogleId { get; set; }

    // Authentication tokens are hashed before being persisted.
    public string? RefreshTokenHash { get; set; }

    public DateTime? RefreshTokenExpiresAt { get; set; }

    public string? EmailVerificationTokenHash { get; set; }

    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    public string? PasswordResetOtpHash { get; set; }

    public DateTime? PasswordResetOtpExpiresAt { get; set; }

    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    /// Active | Inactive | Suspended
    public UserStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth? Booth { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CustomerPreference> CustomerPreferences { get; set; } = new List<CustomerPreference>();

    public virtual CustomerFoodProfile? CustomerFoodProfile { get; set; }

    public virtual ICollection<AiRecommendationSession> AiRecommendationSessions { get; set; } = new List<AiRecommendationSession>();

    public virtual ICollection<AiMealPlanSession> AiMealPlanSessions { get; set; } = new List<AiMealPlanSession>();

    public virtual ICollection<MarketSubscription> MarketSubscriptions { get; set; } = new List<MarketSubscription>();

    public virtual ICollection<AIRecommendationLog> AIRecommendationLogs { get; set; } = new List<AIRecommendationLog>();

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<UserDeviceToken> DeviceTokens { get; set; } = new List<UserDeviceToken>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();

    public virtual ICollection<ReviewReply> ReviewReplies { get; set; } = new List<ReviewReply>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual Role Role { get; set; } = null!;

    public virtual User? CreatedByMarketOwner { get; set; }

    public virtual ICollection<User> CreatedBoothOwnerAccounts { get; set; } = new List<User>();

    public virtual ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();
}
