using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfrastructureLayer.Data
{
    public class SNMDbContext : DbContext
    {
        public SNMDbContext(DbContextOptions<SNMDbContext> options) : base(options)
        {
        }

        // DbSets
        public virtual DbSet<AIRecommendationLog> AIRecommendationLogs { get; set; }

        public virtual DbSet<Booth> Booths { get; set; }

        public virtual DbSet<BoothDocument> BoothDocuments { get; set; }

        public virtual DbSet<BoothImage> BoothImages { get; set; }

        public virtual DbSet<BoothLocation> BoothLocations { get; set; }

        public virtual DbSet<BoothPaymentInfo> BoothPaymentInfos { get; set; }

        public virtual DbSet<BoothRegistration> BoothRegistrations { get; set; }

        public virtual DbSet<BoothSubscription> BoothSubscriptions { get; set; }

        public virtual DbSet<MarketSubscription> MarketSubscriptions { get; set; }

        public virtual DbSet<Cart> Carts { get; set; }

        public virtual DbSet<CartItem> CartItems { get; set; }

        public virtual DbSet<Complaint> Complaints { get; set; }

        public virtual DbSet<ComplaintImage> ComplaintImages { get; set; }

        public virtual DbSet<Conversation> Conversations { get; set; }

        public virtual DbSet<CustomerPreference> CustomerPreferences { get; set; }

        public virtual DbSet<FoodCategory> FoodCategories { get; set; }

        public virtual DbSet<FoodImage> FoodImages { get; set; }

        public virtual DbSet<FoodItem> FoodItems { get; set; }

        public virtual DbSet<FoodItemTag> FoodItemTags { get; set; }

        public virtual DbSet<FoodPrice> FoodPrices { get; set; }

        public virtual DbSet<FoodTag> FoodTags { get; set; }

        public virtual DbSet<LayoutEdge> LayoutEdges { get; set; }

        public virtual DbSet<LayoutNode> LayoutNodes { get; set; }

        public virtual DbSet<MarketLayout> MarketLayouts { get; set; }

        public virtual DbSet<Message> Messages { get; set; }

        public virtual DbSet<NightMarket> NightMarkets { get; set; }

        public virtual DbSet<Notification> Notifications { get; set; }

        public virtual DbSet<Order> Orders { get; set; }

        public virtual DbSet<OrderDetail> OrderDetails { get; set; }

        public virtual DbSet<Payment> Payments { get; set; }

        public virtual DbSet<Package> Packages { get; set; }

        public virtual DbSet<PackagePrice> PackagePrices { get; set; }

        public virtual DbSet<Promotion> Promotions { get; set; }

        public virtual DbSet<PromotionCategory> PromotionCategories { get; set; }

        public virtual DbSet<PromotionFoodItem> PromotionFoodItems { get; set; }

        public virtual DbSet<PromotionUsage> PromotionUsages { get; set; }

        public virtual DbSet<Review> Reviews { get; set; }

        public virtual DbSet<ReviewReply> ReviewReplies { get; set; }

        public virtual DbSet<Role> Roles { get; set; }

        public virtual DbSet<User> Users { get; set; }

        public virtual DbSet<UserDeviceToken> UserDeviceTokens { get; set; }

        public virtual DbSet<Zone> Zones { get; set; }

        public virtual DbSet<UserStatusHistory> UserStatusHistories { get; set; }

        public virtual DbSet<EmailOutbox> EmailOutboxes { get; set; }

        public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }

        public virtual DbSet<SystemSetting> SystemSettings { get; set; }
        public virtual DbSet<PackagePolicy> PackagePolicies { get; set; }

    public virtual DbSet<ModerationActionHistory> ModerationActionHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Apply all configurations from the current assembly
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.Entity<AIRecommendationLog>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("AIRecommendationLog_pkey");

                entity.ToTable("AIRecommendationLog", tb => tb.HasComment("Log tá»‘i giáº£n cho cÃ¡c láº§n AI recommendation Ä‘á»ƒ debug/demo"));

                entity.HasIndex(e => e.CustomerId, "idx_airecommendationlog_customer");
                entity.HasIndex(e => e.NightMarketId, "idx_airecommendationlog_nightmarket");
                entity.HasIndex(e => new { e.RecommendationType, e.CreatedAt }, "idx_airecommendationlog_type_created");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.RecommendationType)
                    .HasConversion<string>()
                    .HasMaxLength(30);
                entity.Property(e => e.InputJson).HasColumnType("jsonb");
                entity.Property(e => e.ParsedIntentJson).HasColumnType("jsonb");
                entity.Property(e => e.ResultJson).HasColumnType("jsonb");
                entity.Property(e => e.SelectedOptionId).HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Customer).WithMany(p => p.AIRecommendationLogs)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("AIRecommendationLog_CustomerId_fkey");

                entity.HasOne(d => d.NightMarket).WithMany(p => p.AIRecommendationLogs)
                    .HasForeignKey(d => d.NightMarketId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("AIRecommendationLog_NightMarketId_fkey");
            });

            modelBuilder.Entity<Cart>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Cart_pkey");

                entity.ToTable("Cart", tb => tb.HasComment("Giá» hÃ ng hiá»‡n táº¡i cá»§a khÃ¡ch hÃ ng"));

                entity.HasIndex(e => e.CustomerId, "ux_cart_active_customer")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Customer).WithMany(p => p.Carts)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Cart_CustomerId_fkey");
            });

            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CartItem_pkey");

                entity.ToTable("CartItem", tb => tb.HasComment("MÃ³n Äƒn trong giá» hÃ ng"));

                entity.HasIndex(e => e.CartId, "idx_cartitem_cart");
                entity.HasIndex(e => e.FoodItemId, "idx_cartitem_fooditem");
                entity.HasIndex(e => new { e.CartId, e.FoodItemId }, "ux_cartitem_active_food")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Quantity);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                    .HasForeignKey(d => d.CartId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("CartItem_CartId_fkey");

                entity.HasOne(d => d.FoodItem).WithMany(p => p.CartItems)
                    .HasForeignKey(d => d.FoodItemId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("CartItem_FoodItemId_fkey");
            });

            modelBuilder.Entity<Booth>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Booth_pkey");

                entity.ToTable("Booth", tb => tb.HasComment("Gian hÃ ng áº©m thá»±c - thá»±c thá»ƒ trung tÃ¢m, má»—i gian hÃ ng thuá»™c 1 NightMarket vÃ  do 1 User (BoothOwner) quáº£n lÃ½"));

                entity.HasIndex(e => e.NightMarketId, "idx_booth_nightmarket");

                entity.HasIndex(e => e.BoothOwnerId, "uq_booth_owner").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.AverageRating)
                    .HasPrecision(3, 2)
                    .HasDefaultValueSql("0")
                    .HasComment("Cache Ä‘iá»ƒm trung bÃ¬nh review, cáº­p nháº­t qua trigger hoáº·c job Ä‘á»‹nh ká»³");
                entity.Property(e => e.BoothName).HasMaxLength(200);
                entity.Property(e => e.BoothCode).HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsFeatured).HasDefaultValue(false);
                entity.Property(e => e.Latitude).HasPrecision(10, 7);
                entity.Property(e => e.Longitude).HasPrecision(10, 7);
                entity.Property(e => e.MapPositionX).HasPrecision(10, 2);
                entity.Property(e => e.MapPositionY).HasPrecision(10, 2);
                entity.Property(e => e.PackageName).HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Pending'::character varying")
                    .HasComment("Pending: chá» Admin duyá»‡t | Active: hoáº¡t Ä‘á»™ng | Inactive: táº¡m ngá»«ng | Suspended: bá»‹ khÃ³a do vi pháº¡m");
                entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.BoothOwner).WithOne(p => p.Booth)
                    .HasForeignKey<Booth>(d => d.BoothOwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Booth_BoothOwnerId_fkey");

                entity.HasOne(d => d.NightMarket).WithMany(p => p.Booths)
                    .HasForeignKey(d => d.NightMarketId)
                    .HasConstraintName("Booth_NightMarketId_fkey");
            });

            modelBuilder.Entity<BoothRegistration>(entity =>
            {
                entity.HasIndex(e => e.OwnerId, "uq_pending_booth_registration_owner")
                    .IsUnique()
                    .HasFilter("\"Status\" = 1");
            });

            modelBuilder.Entity<BoothDocument>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("BoothDocuments_pkey");

                entity.ToTable(tb => tb.HasComment("Giáº¥y tá» phÃ¡p lÃ½ cá»§a gian hÃ ng Ä‘á»ƒ Admin xÃ¡c minh trÆ°á»›c khi cho phÃ©p hoáº¡t Ä‘á»™ng"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.DocumentType)
                    .HasConversion<string>()
                    .HasMaxLength(50);
                entity.Property(e => e.FileUrl).HasMaxLength(500);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.VerificationStatus)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Pending'::character varying");

                entity.HasOne(d => d.Registration).WithMany(p => p.BoothDocuments)
                    .HasForeignKey(d => d.RegistrationId)
                    .HasConstraintName("BoothDocuments_BoothId_fkey");
            });

            modelBuilder.Entity<BoothImage>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("BoothImages_pkey");

                entity.ToTable(tb => tb.HasComment("ThÆ° viá»‡n áº£nh (gallery) cá»§a gian hÃ ng"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.BoothImages)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("BoothImages_BoothId_fkey");
            });

            modelBuilder.Entity<BoothLocation>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("BoothLocations_pkey");

                entity.ToTable(tb => tb.HasComment("Vá»‹ trÃ­ cá»¥ thá»ƒ (tá»a Ä‘á»™) cá»§a 1 gian hÃ ng trÃªn 1 sÆ¡ Ä‘á»“ máº·t báº±ng"));

                entity.HasIndex(e => e.BoothId, "ux_boothlocation_active_booth").IsUnique().HasFilter("\"IsDeleted\" = false");
                entity.HasIndex(e => e.LayoutNodeId, "ux_boothlocation_active_node").IsUnique().HasFilter("\"IsDeleted\" = false");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.SlotNumber).HasMaxLength(50);
                entity.Property(e => e.Xcoordinate)
                    .HasPrecision(10, 2)
                    .HasColumnName("XCoordinate");
                entity.Property(e => e.Ycoordinate)
                    .HasPrecision(10, 2)
                    .HasColumnName("YCoordinate");

                entity.HasOne(d => d.Booth).WithMany(p => p.BoothLocations)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("BoothLocations_BoothId_fkey");

                entity.HasOne(d => d.Layout).WithMany(p => p.BoothLocations)
                    .HasForeignKey(d => d.LayoutId)
                    .HasConstraintName("BoothLocations_LayoutId_fkey");

                entity.HasOne(d => d.LayoutNode).WithMany(p => p.BoothLocations)
                    .HasForeignKey(d => d.LayoutNodeId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("BoothLocations_LayoutNodeId_fkey");

                entity.HasOne(d => d.Zone).WithMany(p => p.BoothLocations)
                    .HasForeignKey(d => d.ZoneId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("BoothLocations_ZoneId_fkey");
            });

            modelBuilder.Entity<BoothPaymentInfo>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("BoothPaymentInfos_pkey");

                entity.ToTable(tb => tb.HasComment("ThÃ´ng tin tÃ i khoáº£n/QR nháº­n thanh toÃ¡n cá»§a gian hÃ ng"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.BankAccountHolder).HasMaxLength(150);
                entity.Property(e => e.BankAccountNumber).HasMaxLength(50);
                entity.Property(e => e.BankName).HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsDefault).HasDefaultValue(false);
                entity.Property(e => e.PaymentType)
                    .HasMaxLength(20)
                    .HasComment("BankTransfer | VNPay | MoMo | ZaloPay | Payos");
                entity.Property(e => e.QrimageUrl)
                    .HasMaxLength(500)
                    .HasColumnName("QRImageUrl");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Draft'::character varying");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.BoothPaymentInfos)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("BoothPaymentInfos_BoothId_fkey");
            });
            modelBuilder.Entity<BoothSubscription>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("BoothSubscriptions_pkey");

                entity.ToTable(tb => tb.HasComment("Lá»‹ch sá»­ Ä‘Äƒng kÃ½ gÃ³i dá»‹ch vá»¥ cá»§a gian hÃ ng"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying")
                    .HasComment("Active | Expired | Cancelled");
                entity.Property(e => e.PaidAmount).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.PayOSOrderCode).HasComment("PayOS order code for payment tracking");
                entity.Property(e => e.PayOSPaymentLinkId).HasMaxLength(100).HasComment("PayOS payment link ID");
                entity.Property(e => e.PaidAt).HasComment("Timestamp when payment was confirmed via PayOS webhook");
                entity.Property(e => e.PaymentExpiresAt).HasComment("PayOS payment link expiration time");
                entity.HasIndex(e => e.PayOSOrderCode).IsUnique().HasDatabaseName("IX_BoothSubscriptions_PayOSOrderCode");

                entity.HasOne(d => d.Booth).WithMany(p => p.BoothSubscriptions)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("BoothSubscriptions_BoothId_fkey");

                entity.HasOne(d => d.Package).WithMany(p => p.BoothSubscriptions)
                    .HasForeignKey(d => d.PackageId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("BoothSubscriptions_PackageId_fkey");

                entity.Property(e => e.PolicyVersion).HasMaxLength(50);
                entity.Property(e => e.ChangeType).HasMaxLength(50);
                entity.Property(e => e.CreditAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.BuyerName).HasMaxLength(200);
                entity.Property(e => e.BuyerEmail).HasMaxLength(200);
                entity.Property(e => e.BuyerPhone).HasMaxLength(20);
});

            modelBuilder.Entity<MarketSubscription>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("MarketSubscriptions_pkey");

                entity.ToTable(tb => tb.HasComment("Lá»‹ch sá»­ Ä‘Äƒng kÃ½ gÃ³i dá»‹ch vá»¥ cá»§a Market Owner"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying")
                    .HasComment("Active | Expired | Cancelled | PendingPayment");
                entity.Property(e => e.PaidAmount).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.PayOSOrderCode).HasComment("PayOS order code for payment tracking");
                entity.Property(e => e.PayOSPaymentLinkId).HasMaxLength(100).HasComment("PayOS payment link ID");
                entity.Property(e => e.PaidAt).HasComment("Timestamp when payment was confirmed via PayOS webhook");
                entity.Property(e => e.PaymentExpiresAt).HasComment("PayOS payment link expiration time");
                entity.HasIndex(e => e.PayOSOrderCode).IsUnique().HasDatabaseName("IX_MarketSubscriptions_PayOSOrderCode");

                entity.HasOne(d => d.MarketOwner).WithMany(p => p.MarketSubscriptions)
                    .HasForeignKey(d => d.MarketOwnerId)
                    .HasConstraintName("MarketSubscriptions_MarketOwnerId_fkey");

                entity.HasOne(d => d.Package).WithMany(p => p.MarketSubscriptions)
                    .HasForeignKey(d => d.PackageId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("MarketSubscriptions_PackageId_fkey");

                entity.Property(e => e.PolicyVersion).HasMaxLength(50);
                entity.Property(e => e.ChangeType).HasMaxLength(50);
                entity.Property(e => e.CreditAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.BuyerName).HasMaxLength(200);
                entity.Property(e => e.BuyerEmail).HasMaxLength(200);
                entity.Property(e => e.BuyerPhone).HasMaxLength(20);
});

            modelBuilder.Entity<Complaint>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Complaints_pkey");

                entity.ToTable(tb => tb.HasComment("Khiáº¿u náº¡i cá»§a khÃ¡ch hÃ ng vá» Ä‘Æ¡n hÃ ng/gian hÃ ng"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Pending'::character varying")
                    .HasComment("Pending | Resolved | Rejected");
                entity.Property(e => e.ResolutionAction)
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .HasComment("NoViolation | Warning | SuspendBooth | CloseBooth");
                entity.Property(e => e.PolicyViolation).HasMaxLength(500);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.Complaints)
                    .HasForeignKey(d => d.BoothId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Complaints_BoothId_fkey");

                entity.HasOne(d => d.Customer).WithMany(p => p.Complaints)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Complaints_CustomerId_fkey");

                entity.HasOne(d => d.Order).WithMany(p => p.Complaints)
                    .HasForeignKey(d => d.OrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Complaints_OrderId_fkey");
            });

            modelBuilder.Entity<ComplaintImage>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("ComplaintImages_pkey");

                entity.ToTable(tb => tb.HasComment("áº¢nh minh chá»©ng Ä‘Ã­nh kÃ¨m theo khiáº¿u náº¡i"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Complaint).WithMany(p => p.ComplaintImages)
                    .HasForeignKey(d => d.ComplaintId)
                    .HasConstraintName("ComplaintImages_ComplaintId_fkey");
            });

            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Conversations_pkey");

                entity.ToTable(tb => tb.HasComment("Cuá»™c trÃ² chuyá»‡n giá»¯a 1 khÃ¡ch hÃ ng vÃ  1 gian hÃ ng - dÃ¹ng SignalR Ä‘á»ƒ realtime"));

                entity.HasIndex(e => new { e.CustomerId, e.BoothOwnerId }, "uq_conversation_customer_boothowner").IsUnique();
                entity.HasIndex(e => e.LastMessageAt, "idx_conversation_last_message");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValue(DomainLayer.Enums.GeneralEnum.ConversationStatus.Active);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.BoothOwner).WithMany()
                    .HasForeignKey(d => d.BoothOwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Conversations_BoothOwnerId_fkey");

                entity.HasOne(d => d.Customer).WithMany(p => p.Conversations)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Conversations_CustomerId_fkey");

                entity.HasOne(d => d.LastMessage).WithMany()
                    .HasForeignKey(d => d.LastMessageId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("Conversations_LastMessageId_fkey");
            });

            modelBuilder.Entity<CustomerPreference>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("CustomerPreference_pkey");

                entity.ToTable("CustomerPreference", tb => tb.HasComment("Sá»Ÿ thÃ­ch rÃµ rÃ ng cá»§a khÃ¡ch hÃ ng theo FoodTag: Like/Avoid"));

                entity.HasIndex(e => e.CustomerId, "idx_customerpreference_customer");
                entity.HasIndex(e => e.FoodTagId, "idx_customerpreference_foodtag");
                entity.HasIndex(e => new { e.CustomerId, e.FoodTagId, e.PreferenceKind }, "ux_customerpreference_tag_kind")
                    .IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.PreferenceKind)
                    .HasConversion<string>()
                    .HasMaxLength(20);
                entity.Property(e => e.PreferenceSource)
                    .HasConversion<string>()
                    .HasMaxLength(30);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Customer).WithMany(p => p.CustomerPreferences)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("CustomerPreference_CustomerId_fkey");

                entity.HasOne(d => d.FoodTag).WithMany(p => p.CustomerPreferences)
                    .HasForeignKey(d => d.FoodTagId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("CustomerPreference_FoodTagId_fkey");
            });

            modelBuilder.Entity<FoodCategory>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("FoodCategories_pkey");

                entity.ToTable(tb => tb.HasComment("Danh má»¥c mÃ³n Äƒn cá»§a tá»«ng gian hÃ ng"));

                entity.HasIndex(e => e.BoothId, "idx_foodcategory_booth");

                entity.HasIndex(e => new { e.BoothId, e.Name }, "FoodCategories_BoothId_Name_key")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.FoodCategories)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("FoodCategories_BoothId_fkey");
            });

            modelBuilder.Entity<FoodImage>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("FoodImages_pkey");

                entity.ToTable(tb => tb.HasComment("ThÆ° viá»‡n áº£nh (gallery) cho tá»«ng mÃ³n Äƒn"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.FoodItem).WithMany(p => p.FoodImages)
                    .HasForeignKey(d => d.FoodItemId)
                    .HasConstraintName("FoodImages_FoodItemId_fkey");
            });

            modelBuilder.Entity<FoodItem>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("FoodItem_pkey");

                entity.ToTable("FoodItem", tb => tb.HasComment("MÃ³n Äƒn cá»§a tá»«ng gian hÃ ng"));

                entity.HasIndex(e => e.BoothId, "idx_fooditem_booth");

                entity.HasIndex(e => e.CategoryId, "idx_fooditem_category");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsAvailable)
                    .HasDefaultValue(true)
                    .HasComment("false khi mÃ³n háº¿t nguyÃªn liá»‡u hoáº·c chá»§ quÃ¡n táº¡m áº©n");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.IsFeatured).HasDefaultValue(false);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Price)
                    .HasPrecision(12, 2)
                    .HasComment("GiÃ¡ máº·c Ä‘á»‹nh. Náº¿u cÃ³ FoodPrice theo ngÃ y hiá»‡n táº¡i thÃ¬ giÃ¡ Ä‘Ã³ Ä‘Æ°á»£c Æ°u tiÃªn (override)");
                entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.FoodItems)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("FoodItem_BoothId_fkey");

                entity.HasOne(d => d.Category).WithMany(p => p.FoodItems)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FoodItem_CategoryId_fkey");

            });

            modelBuilder.Entity<FoodItemTag>(entity =>
            {
                entity.HasKey(e => new { e.FoodItemId, e.FoodTagId })
                    .HasName("FoodItemTag_pkey");

                entity.ToTable("FoodItemTag", tb => tb.HasComment("Báº£ng ná»‘i gáº¯n tag ngá»¯ nghÄ©a vÃ o mÃ³n Äƒn"));

                entity.HasIndex(e => e.FoodTagId, "idx_fooditemtag_foodtag");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.FoodItem).WithMany(p => p.FoodItemTags)
                    .HasForeignKey(d => d.FoodItemId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FoodItemTag_FoodItemId_fkey");

                entity.HasOne(d => d.FoodTag).WithMany(p => p.FoodItemTags)
                    .HasForeignKey(d => d.FoodTagId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FoodItemTag_FoodTagId_fkey");
            });

            modelBuilder.Entity<FoodPrice>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("FoodPrice_pkey");

                entity.ToTable("FoodPrice", tb => tb.HasComment("Báº£ng giÃ¡ theo ngÃ y trong tuáº§n - override giÃ¡ máº·c Ä‘á»‹nh cá»§a FoodItem"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.Price).HasPrecision(12, 2);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.FoodItem).WithMany(p => p.FoodPrices)
                    .HasForeignKey(d => d.FoodItemId)
                    .HasConstraintName("FoodPrice_FoodItemId_fkey");
            });

            modelBuilder.Entity<FoodTag>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("FoodTag_pkey");

                entity.ToTable("FoodTag", tb => tb.HasComment("Danh sÃ¡ch tag chuáº©n mÃ´ táº£ ngá»¯ nghÄ©a mÃ³n Äƒn cho AI/recommendation"));

                entity.HasIndex(e => e.Code, "ux_foodtag_code_active")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");
                entity.HasIndex(e => e.Name, "ux_foodtag_name_active")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.Code).HasMaxLength(100);
                entity.Property(e => e.TagGroup)
                    .HasConversion<string>()
                    .HasMaxLength(30);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<LayoutEdge>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("LayoutEdges_pkey");

                entity.ToTable(tb => tb.HasComment("Cáº¡nh ná»‘i giá»¯a 2 LayoutNode - thá»ƒ hiá»‡n Ä‘Æ°á»ng Ä‘i vÃ  khoáº£ng cÃ¡ch, dÃ¹ng cho thuáº­t toÃ¡n tÃ¬m Ä‘Æ°á»ng ngáº¯n nháº¥t trong chá»£"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Distance).HasPrecision(10, 2);
                entity.Property(e => e.IsBidirectional).HasDefaultValue(true);
                entity.Property(e => e.IsAccessible).HasDefaultValue(true);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasIndex(e => new { e.LayoutId, e.FromNodeId, e.ToNodeId }, "ux_layoutedge_active")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.HasOne(d => d.Layout).WithMany(p => p.LayoutEdges)
                    .HasForeignKey(d => d.LayoutId)
                    .HasConstraintName("LayoutEdges_LayoutId_fkey");
                entity.HasOne(d => d.FromNode).WithMany(p => p.OutgoingEdges)
                    .HasForeignKey(d => d.FromNodeId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("LayoutEdges_FromNodeId_fkey");
                entity.HasOne(d => d.ToNode).WithMany(p => p.IncomingEdges)
                    .HasForeignKey(d => d.ToNodeId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("LayoutEdges_ToNodeId_fkey");
            });

            modelBuilder.Entity<LayoutNode>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("LayoutNodes_pkey");

                entity.ToTable(tb => tb.HasComment("CÃ¡c Ä‘iá»ƒm/nÃºt (node) trÃªn sÆ¡ Ä‘á»“ máº·t báº±ng - lÃ  Ä‘á»‰nh cá»§a Ä‘á»“ thá»‹ dÃ¹ng cho tÃ¬m Ä‘Æ°á»ng ná»™i bá»™ chá»£"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.NodeName).HasMaxLength(100);
                entity.Property(e => e.NodeType)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Junction'::character varying");
                entity.Property(e => e.IsAccessible).HasDefaultValue(true);
                entity.Property(e => e.IsStartingPoint).HasDefaultValue(false);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Xcoordinate)
                    .HasPrecision(10, 2)
                    .HasColumnName("XCoordinate");
                entity.Property(e => e.Ycoordinate)
                    .HasPrecision(10, 2)
                    .HasColumnName("YCoordinate");

                entity.HasOne(d => d.Layout).WithMany(p => p.LayoutNodes)
                    .HasForeignKey(d => d.LayoutId)
                    .HasConstraintName("LayoutNodes_LayoutId_fkey");

                entity.HasOne(d => d.Zone).WithMany(p => p.LayoutNodes)
                    .HasForeignKey(d => d.ZoneId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("LayoutNodes_ZoneId_fkey");
            });

            modelBuilder.Entity<MarketLayout>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("MarketLayouts_pkey");

                entity.ToTable(tb => tb.HasComment("SÆ¡ Ä‘á»“ máº·t báº±ng cá»§a má»™t chá»£ Ä‘Ãªm - dÃ¹ng lÃ m ná»n Ä‘á»ƒ Ä‘áº·t cÃ¡c Ä‘iá»ƒm (LayoutNodes) vÃ  gian hÃ ng (BoothLocations)"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.LayoutName).HasMaxLength(150);
                entity.Property(e => e.LayoutImageUrl).HasMaxLength(500);
                entity.Property(e => e.Version).HasDefaultValue(1);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Draft'::character varying");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasIndex(e => new { e.NightMarketId, e.LayoutName }, "ux_marketlayout_market_name_active")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");
                entity.HasIndex(e => new { e.NightMarketId, e.Version }, "ux_marketlayout_market_version_active")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");
                entity.HasIndex(e => e.NightMarketId, "ux_marketlayout_one_active_per_market")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false AND \"Status\" = 'Active'");

                entity.HasOne(d => d.NightMarket).WithMany(p => p.MarketLayouts)
                    .HasForeignKey(d => d.NightMarketId)
                    .HasConstraintName("MarketLayouts_NightMarketId_fkey");
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Message_pkey");

                entity.ToTable("Message", tb => tb.HasComment("Tin nháº¯n trong cuá»™c trÃ² chuyá»‡n - truyá»n táº£i qua SignalR Hub"));

                entity.HasIndex(e => new { e.ConversationId, e.CreatedAt }, "idx_message_conversation");
                entity.HasIndex(e => new { e.SenderId, e.ClientMessageId }, "ux_message_sender_client_message")
                    .IsUnique()
                    .HasFilter("\"ClientMessageId\" IS NOT NULL");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                entity.Property(e => e.Type)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Text'::character varying")
                    .HasComment("Text | Image | System");
                entity.Property(e => e.SenderRole)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasComment("Snapshot vai trÃ² ngÆ°á»i gá»­i: Customer | BoothOwner");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                    .HasForeignKey(d => d.ConversationId)
                    .HasConstraintName("Message_ConversationId_fkey");

                entity.HasOne(d => d.Sender).WithMany(p => p.Messages)
                    .HasForeignKey(d => d.SenderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Message_SenderId_fkey");
            });

            modelBuilder.Entity<NightMarket>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("NightMarket_pkey");

                entity.ToTable("NightMarket", tb => tb.HasComment("ThÃ´ng tin cÃ¡c chá»£ Ä‘Ãªm - Ä‘Æ¡n vá»‹ quáº£n lÃ½ cáº¥p cao nháº¥t, chá»©a nhiá»u Booth"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.HasIndex(e => new { e.IsDeleted, e.Status, e.CreatedAt }, "idx_nightmarket_active_status_created");

                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.Latitude).HasPrecision(10, 7);
                entity.Property(e => e.Longitude).HasPrecision(10, 7);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying");
                entity.Property(e => e.ModerationStatus)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying");
                entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
                entity.Property(e => e.TotalBooth)
                    .HasDefaultValue(0)
                    .HasComment("Sá»‘ lÆ°á»£ng gian hÃ ng - giÃ¡ trá»‹ cache, Ä‘á»“ng bá»™ qua trigger hoáº·c job Ä‘á»‹nh ká»³");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.MarketOwner)
                    .WithMany()
                    .HasForeignKey(d => d.MarketOwnerId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("NightMarket_MarketOwnerId_fkey");
            });

            modelBuilder.Entity<ModerationActionHistory>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("ModerationActionHistory_pkey");

                entity.ToTable("ModerationActionHistory", tb => tb.HasComment("Lá»‹ch sá»­ hÃ nh Ä‘á»™ng moderation cho Booth hoáº·c Night Market"));

                entity.HasIndex(e => e.BoothId, "idx_moderationhistory_booth");
                entity.HasIndex(e => e.NightMarketId, "idx_moderationhistory_nightmarket");
                entity.HasIndex(e => e.CreatedAt, "idx_moderationhistory_created");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.AdminName).HasMaxLength(200);
                entity.Property(e => e.PreviousStatus).HasMaxLength(20);
                entity.Property(e => e.NewStatus).HasMaxLength(20);
                entity.Property(e => e.Reason).HasMaxLength(1000);
                entity.Property(e => e.Source)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'DirectAdmin'::character varying");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth)
                    .WithMany()
                    .HasForeignKey(d => d.BoothId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("ModerationActionHistory_BoothId_fkey");

                entity.HasOne(d => d.NightMarket)
                    .WithMany()
                    .HasForeignKey(d => d.NightMarketId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("ModerationActionHistory_NightMarketId_fkey");
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Notification_pkey");

                entity.ToTable("Notification", tb => tb.HasComment("ThÃ´ng bÃ¡o Ä‘áº©y (push notification qua FCM) cho ngÆ°á»i dÃ¹ng"));

                entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_notification_user").IsDescending(false, true);

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.BoothId).HasComment("NULL khi thÃ´ng bÃ¡o khÃ´ng gáº¯n vá»›i gian hÃ ng cá»¥ thá»ƒ (VD: thÃ´ng bÃ¡o há»‡ thá»‘ng)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                entity.Property(e => e.ReferenceType).HasMaxLength(100);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.Type)
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .HasComment("NewOrder | OrderStatusChanged | NewMessage | Promotion | System");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.Notifications)
                    .HasForeignKey(d => d.BoothId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("Notification_BoothId_fkey");

                entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("Notification_UserId_fkey");
            });

            modelBuilder.Entity<UserDeviceToken>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("UserDeviceToken_pkey");
                entity.ToTable("UserDeviceToken", tb => tb.HasComment("FCM device tokens registered by users"));

                entity.HasIndex(e => e.Token, "UserDeviceToken_Token_key").IsUnique();
                entity.HasIndex(e => new { e.UserId, e.IsActive }, "idx_device_token_user_active");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Token).HasMaxLength(4096);
                entity.Property(e => e.DeviceId).HasMaxLength(200);
                entity.Property(e => e.Platform)
                    .HasConversion<string>()
                    .HasMaxLength(20);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(e => e.User)
                    .WithMany(user => user.DeviceTokens)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("UserDeviceToken_UserId_fkey");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Order_pkey");

                entity.ToTable("Order", tb => tb.HasComment("ÄÆ¡n hÃ ng cá»§a khÃ¡ch (1 Ä‘Æ¡n chá»‰ thuá»™c vá» 1 quÃ¡n)"));

                entity.HasIndex(e => e.OrderCode, "Order_OrderCode_key").IsUnique();

                entity.HasIndex(e => e.CustomerId, "idx_order_customer");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.DiscountAmount).HasPrecision(12, 2);
                entity.Property(e => e.FinalAmount)
                    .HasPrecision(12, 2)
                    .HasComment("TotalAmount - DiscountAmount");
                entity.Property(e => e.OrderCode)
                    .IsRequired();
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .HasDefaultValueSql("'Placed'::character varying")
                    .HasComment("Placed | Preparing | ReadyForPickup | Completed | Cancelled");
                entity.Property(e => e.TotalAmount).HasPrecision(12, 2);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Order_CustomerId_fkey");

                entity.HasOne(o => o.BoothOwner)
                    .WithMany()
                    .HasForeignKey(o => o.BoothOwnerId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("Order_BoothOwnerId_fkey");
            });

            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("OrderDetail_pkey");

                entity.ToTable("OrderDetail", tb => tb.HasComment("Chi tiáº¿t mÃ³n Äƒn trong tá»«ng Ä‘Æ¡n hÃ ng"));

                entity.HasIndex(e => e.OrderId, "idx_orderdetail_order");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.TotalPrice).HasPrecision(12, 2);
                entity.Property(e => e.UnitPrice)
                    .HasPrecision(12, 2)
                    .HasComment("SNAPSHOT giÃ¡ táº¡i thá»i Ä‘iá»ƒm Ä‘áº·t hÃ ng - KHÃ”NG tÃ­nh láº¡i tá»« FoodItem.Price");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.FoodItem).WithMany(p => p.OrderDetails)
                    .HasForeignKey(d => d.FoodItemId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("OrderDetail_FoodItemId_fkey");

                entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                    .HasForeignKey(d => d.OrderId)
                    .HasConstraintName("OrderDetail_OrderId_fkey");
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Payments_pkey");

                entity.ToTable(tb => tb.HasComment("Lá»‹ch sá»­ giao dá»‹ch thanh toÃ¡n/hoÃ n tiá»n - tÃ­ch há»£p Ä‘a cá»•ng VNPay/ZaloPay/MoMo/Payos"));

                entity.HasIndex(e => e.OrderId, "idx_payments_order");

                entity.HasIndex(e => e.PayOSOrderCode, "idx_payments_payos_ordercode")
                    .IsUnique()
                    .HasFilter("\"PayOSOrderCode\" IS NOT NULL");

                entity.HasIndex(e => e.OrderId, "ux_payments_one_pending_payos_per_order")
                    .IsUnique()
                    .HasFilter("\"Status\" = 'Pending' AND \"Gateway\" = 'Payos'");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Amount).HasPrecision(12, 2);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Gateway)
                    .HasConversion<string>()
                    .HasMaxLength(20);
                //entity.Property(e => e.Currency)
                //    .HasMaxLength(10)
                //    .HasDefaultValueSql("'VND'::character varying");
                //entity.Property(e => e.Gateway).HasMaxLength(20);
                entity.Property(e => e.CheckoutUrl)
                    .HasMaxLength(2000)
                    .HasComment("ÄÆ°á»ng link thanh toÃ¡n VietQR Ä‘á»™ng ngáº¯n háº¡n do PayOS tráº£ vá»");
                entity.Property(e => e.PaymentLinkId)
                    .HasMaxLength(255)
                    .HasComment("ID quáº£n lÃ½ liÃªn káº¿t link thanh toÃ¡n cá»§a há»‡ thá»‘ng PayOS");
                entity.Property(e => e.GatewayRef)
                    .HasMaxLength(255)
                    .HasComment("MÃ£ tra soÃ¡t thá»±c táº¿ cá»§a ngÃ¢n hÃ ng (VÃ­ dá»¥ mÃ£ giao dá»‹ch cá»§a BIDV...)");
                entity.Property(e => e.PayOSOrderCode)
                    .HasComment("PayOS order code for this specific payment transaction (supplemental payments)");
                entity.Property(e => e.RefundReason)
                    .HasMaxLength(500)
                    .HasComment("LÃ½ do hoÃ n tiá»n (Náº¿u cÃ³)");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Pending'::character varying");
                entity.Property(e => e.Type)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasComment("Tiá»n máº·t hoáº·c PayOS");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.PaidAt)
                    .IsRequired(false) // Ä‘Ã¢y lÃ  trÆ°á»ng Nullable (Ä‘Æ°á»£c phÃ©p trá»‘ng)
                    .HasComment("Thá»i Ä‘iá»ƒm dÃ²ng tiá»n thá»±c táº¿ Ä‘Æ°á»£c khÃ¡ch hÃ ng quÃ©t mÃ£ vÃ  báº¯n vá» há»‡ thá»‘ng thÃ nh cÃ´ng");

                entity.HasOne(d => d.BoothOwner).WithMany(p => p.Payments)
                    .HasForeignKey(d => d.BoothOwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Payments_BoothOwnerId_fkey");

                entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                    .HasForeignKey(d => d.OrderId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("Payments_OrderId_fkey");
            });

            modelBuilder.Entity<Package>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Package_pkey");

                entity.ToTable("Package");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.PackageName).HasMaxLength(100);
                entity.Property(e => e.Code).HasMaxLength(50);
                entity.Property(e => e.Entitlements).HasColumnType("jsonb");
                entity.Property(e => e.Price).HasPrecision(12, 2);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<PackagePrice>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PackagePrice_pkey");

                entity.ToTable("PackagePrice");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.Price).HasPrecision(12, 2);
                entity.Property(e => e.DurationDays).HasDefaultValue(30);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Package).WithMany(p => p.PackagePrices)
                    .HasForeignKey(d => d.PackageId)
                    .HasConstraintName("PackagePrice_PackageId_fkey");
            });

            modelBuilder.Entity<Promotion>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Promotion_pkey");

                entity.ToTable("Promotion", tb => tb.HasComment("ChÆ°Æ¡ng trÃ¬nh khuyáº¿n mÃ£i/mÃ£ giáº£m giÃ¡ do gian hÃ ng táº¡o"));

                entity.HasIndex(e => e.BoothId, "idx_promotion_booth");
                entity.HasIndex(e => new { e.BoothId, e.PromotionCode }, "ux_promotion_active_code")
                    .IsUnique()
                    .HasFilter("\"PromotionCode\" IS NOT NULL AND \"IsDeleted\" = false");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.DiscountType)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasComment("Percentage: giáº£m % | FixedAmount: giáº£m sá»‘ tiá»n cá»‘ Ä‘á»‹nh");
                entity.Property(e => e.DiscountValue).HasPrecision(12, 2);
                entity.Property(e => e.Scope)
                    .HasConversion<string>()
                    .HasMaxLength(30);
                entity.Property(e => e.MinimumOrderAmount).HasPrecision(12, 2);
                entity.Property(e => e.MaximumDiscountAmount).HasPrecision(12, 2);
                entity.Property(e => e.IsPublic).HasDefaultValue(false);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Scheduled'::character varying");
                entity.Property(e => e.PromotionCode).HasMaxLength(50);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.Promotions)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("Promotion_BoothId_fkey");
            });

            modelBuilder.Entity<PromotionFoodItem>(entity =>
            {
                entity.HasKey(e => new { e.PromotionId, e.FoodItemId })
                    .HasName("PromotionFoodItem_pkey");

                entity.ToTable("PromotionFoodItem");

                entity.HasIndex(e => e.FoodItemId, "idx_promotionfooditem_food");

                entity.HasOne(d => d.Promotion)
                    .WithMany(p => p.PromotionFoodItems)
                    .HasForeignKey(d => d.PromotionId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("PromotionFoodItem_PromotionId_fkey");

                entity.HasOne(d => d.FoodItem)
                    .WithMany(p => p.PromotionFoodItems)
                    .HasForeignKey(d => d.FoodItemId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("PromotionFoodItem_FoodItemId_fkey");
            });

            modelBuilder.Entity<PromotionCategory>(entity =>
            {
                entity.HasKey(e => new { e.PromotionId, e.CategoryId })
                    .HasName("PromotionCategory_pkey");

                entity.ToTable("PromotionCategory");

                entity.HasIndex(e => e.CategoryId, "idx_promotioncategory_category");

                entity.HasOne(d => d.Promotion)
                    .WithMany(p => p.PromotionCategories)
                    .HasForeignKey(d => d.PromotionId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("PromotionCategory_PromotionId_fkey");

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.PromotionCategories)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("PromotionCategory_CategoryId_fkey");
            });

            modelBuilder.Entity<PromotionUsage>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PromotionUsages_pkey");

                entity.ToTable(tb => tb.HasComment("Lá»‹ch sá»­ sá»­ dá»¥ng mÃ£ khuyáº¿n mÃ£i - kiá»ƒm tra UsageLimit vÃ  chá»‘ng dÃ¹ng trÃ¹ng"));

                entity.HasIndex(e => new { e.PromotionId, e.OrderId }, "uq_promotionusage_order").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.DiscountAmount).HasPrecision(12, 2);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Reserved'::character varying");
                entity.Property(e => e.AppliedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Customer).WithMany(p => p.PromotionUsages)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("PromotionUsages_CustomerId_fkey");

                entity.HasOne(d => d.Order).WithMany(p => p.PromotionUsages)
                    .HasForeignKey(d => d.OrderId)
                    .HasConstraintName("PromotionUsages_OrderId_fkey");

                entity.HasOne(d => d.Promotion).WithMany(p => p.PromotionUsages)
                    .HasForeignKey(d => d.PromotionId)
                    .HasConstraintName("PromotionUsages_PromotionId_fkey");
            });
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Reviews_pkey");

                entity.ToTable(tb => tb.HasComment("ÄÃ¡nh giÃ¡ cá»§a khÃ¡ch hÃ ng cho gian hÃ ng, gáº¯n liá»n vá»›i 1 Ä‘Æ¡n hÃ ng Ä‘Ã£ hoÃ n táº¥t"));

                entity.HasIndex(e => e.BoothId, "idx_reviews_booth");

                entity.HasIndex(e => e.OrderId, "uq_review_order").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.IsVisible)
                    .HasDefaultValue(true)
                    .HasComment("false: Admin áº©n review nhÆ°ng váº«n giá»¯ dá»¯ liá»‡u Ä‘á»ƒ tÃ­nh rating");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("Reviews_BoothId_fkey");

                entity.HasOne(d => d.Customer).WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Reviews_CustomerId_fkey");

                entity.HasOne(d => d.Order).WithOne(p => p.Review)
                    .HasForeignKey<Review>(d => d.OrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Reviews_OrderId_fkey");
            });

            modelBuilder.Entity<ReviewReply>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("ReviewReplies_pkey");

                entity.ToTable(tb => tb.HasComment("Pháº£n há»“i cá»§a chá»§ gian hÃ ng Ä‘á»‘i vá»›i Ä‘Ã¡nh giÃ¡ - quan há»‡ 1-1 vá»›i Reviews"));

                entity.HasIndex(e => e.ReviewId, "ReviewReplies_ReviewId_key").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.BoothOwner).WithMany(p => p.ReviewReplies)
                    .HasForeignKey(d => d.BoothOwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("ReviewReplies_BoothOwnerId_fkey");

                entity.HasOne(d => d.Review).WithOne(p => p.ReviewReply)
                    .HasForeignKey<ReviewReply>(d => d.ReviewId)
                    .HasConstraintName("ReviewReplies_ReviewId_fkey");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Role_pkey");

                entity.ToTable("Role", tb => tb.HasComment("Danh sÃ¡ch vai trÃ² ngÆ°á»i dÃ¹ng trong há»‡ thá»‘ng (Customer, BoothOwner, Admin)"));

                entity.HasIndex(e => e.RoleName, "Role_RoleName_key").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.RoleName).HasMaxLength(50);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("User_pkey");

                entity.ToTable("User", tb => tb.HasComment("TÃ i khoáº£n ngÆ°á»i dÃ¹ng - dÃ¹ng chung cho Customer, BoothOwner, Admin (phÃ¢n biá»‡t qua RoleId)"));

                entity.HasIndex(e => e.Email, "User_Email_key").IsUnique();

                entity.HasIndex(e => e.UserName, "User_UserName_key").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Address).HasMaxLength(255);
                entity.Property(e => e.AvatarUrl).HasMaxLength(500);
                entity.Property(e => e.AuthProvider)
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .HasDefaultValue(DomainLayer.Enums.GeneralEnum.AuthProvider.Local);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Email).HasMaxLength(150);
                entity.Property(e => e.EmailVerificationTokenHash).HasMaxLength(64);
                entity.Property(e => e.FullName).HasMaxLength(150);
                entity.Property(e => e.GoogleId).HasMaxLength(100);
                entity.Property(e => e.PasswordHash)
                    .HasMaxLength(255)
                    .HasComment("Máº­t kháº©u Ä‘Ã£ Ä‘Æ°á»£c mÃ£ hÃ³a (hash), tuyá»‡t Ä‘á»‘i khÃ´ng lÆ°u plaintext");
                entity.Property(e => e.PasswordResetOtpHash).HasMaxLength(64);
                entity.Property(e => e.PasswordResetTokenHash).HasMaxLength(64);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.RefreshTokenHash).HasMaxLength(64);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasComment("Active: Ä‘ang hoáº¡t Ä‘á»™ng | Inactive: chÆ°a xÃ¡c thá»±c | Banned: bá»‹ khÃ³a bá»Ÿi Admin");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UserName).HasMaxLength(100);

                entity.HasIndex(e => e.GoogleId)
                    .IsUnique()
                    .HasFilter("\"GoogleId\" IS NOT NULL");

                entity.HasIndex(e => e.RefreshTokenHash)
                    .IsUnique()
                    .HasFilter("\"RefreshTokenHash\" IS NOT NULL");

                entity.HasOne(d => d.Role).WithMany(p => p.Users)
                    .HasForeignKey(d => d.RoleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("User_RoleId_fkey");
            });

            modelBuilder.Entity<Zone>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Zones_pkey");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.ZoneName).HasMaxLength(100);
                entity.Property(e => e.Color).HasMaxLength(50);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasIndex(e => new { e.NightMarketId, e.ZoneName }, "ux_zone_market_name_active")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.HasOne(d => d.NightMarket).WithMany(p => p.Zones)
                    .HasForeignKey(d => d.NightMarketId)
                    .HasConstraintName("Zones_NightMarketId_fkey");
            });
            modelBuilder.Entity<UserStatusHistory>(entity =>
            {
                entity.ToTable("UserStatusHistories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.ChangedByAdminId).IsRequired();
                entity.Property(e => e.PreviousStatus)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);
                entity.Property(e => e.NewStatus)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");

                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
                entity.HasIndex(e => e.ChangedByAdminId);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.ChangedByAdmin)
                    .WithMany()
                    .HasForeignKey(e => e.ChangedByAdminId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EmailOutbox>(entity =>
            {
                entity.ToTable("EmailOutbox");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ReferenceId, e.EmailType }).IsUnique();
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.NextRetryAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.SentAt).HasColumnType("timestamp with time zone");
            });

            modelBuilder.Entity<UserStatusHistory>(entity =>
            {
                entity.ToTable("UserStatusHistories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.ChangedByAdminId).IsRequired();
                entity.Property(e => e.PreviousStatus)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);
                entity.Property(e => e.NewStatus)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");

                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
                entity.HasIndex(e => e.ChangedByAdminId);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.ChangedByAdmin)
                    .WithMany()
                    .HasForeignKey(e => e.ChangedByAdminId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EmailOutbox>(entity =>
            {
                entity.ToTable("EmailOutbox");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ReferenceId, e.EmailType }).IsUnique();
                entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.NextRetryAt).HasColumnType("timestamp with time zone");
                entity.Property(e => e.SentAt).HasColumnType("timestamp with time zone");
            });

            modelBuilder.Entity<PaymentMethod>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("PaymentMethod", tb => tb.HasComment("Cáº¥u hÃ¬nh phÆ°Æ¡ng thá»©c thanh toÃ¡n Æ°u tiÃªn cá»§a ngÆ°á»i dÃ¹ng"));

                // Ã‰p kiá»ƒu Enum thÃ nh string Ä‘á»ƒ Ä‘á»“ng bá»™ vá»›i cÃ¡ch lÆ°u cá»§a báº£ng Order vÃ  Payment
                entity.Property(e => e.MethodType)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                // TrÆ°á»ng Token cho phÃ©p null náº¿u khÃ¡ch chá»‰ chá»n phÆ°Æ¡ng thá»©c Cash (Tiá»n máº·t)
                entity.Property(e => e.PaymentToken)
                    .IsRequired(false)
                    .HasMaxLength(500);

                entity.HasOne(pm => pm.User)
                    .WithMany(u => u.PaymentMethods)
                    .HasForeignKey(pm => pm.UserId)
                    .OnDelete(DeleteBehavior.Cascade); // Náº¿u xÃ³a User thÃ¬ tá»± Ä‘á»™ng xÃ³a luÃ´n PaymentMethod cá»§a ngÆ°á»i Ä‘Ã³
            });

            //modelBuilder.Entity<User>().HasData(new User
            //{
            //    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            //    FullName = "KhÃ¡ch VÃ£ng Lai",
            //    Email = "walkincustomer@system.local",
            //    CreatedAt = DateTime.UtcNow
            //});


            modelBuilder.Entity<PackagePolicy>(entity =>
            {
                entity.ToTable("PackagePolicies");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Version).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.ContentJson).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasIndex(e => new { e.PackageId, e.Version }).IsUnique();

                entity.HasIndex(e => new { e.PackageId, e.IsActive })
                    .IsUnique()
                    .HasFilter("\"IsActive\" = true AND \"IsDeleted\" = false");

                entity.HasOne(d => d.Package).WithMany(p => p.Policies)
                    .HasForeignKey(d => d.PackageId)
                    .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);

                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.ToTable("SystemSetting");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.Key).IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Key).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });
        }
    }
}
