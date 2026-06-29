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
        public virtual DbSet<Booth> Booths { get; set; }

        public virtual DbSet<BoothDocument> BoothDocuments { get; set; }

        public virtual DbSet<BoothImage> BoothImages { get; set; }

        public virtual DbSet<BoothLocation> BoothLocations { get; set; }

        public virtual DbSet<BoothPaymentInfo> BoothPaymentInfos { get; set; }

        public virtual DbSet<BoothRegistration> BoothRegistrations { get; set; }

        public virtual DbSet<BoothSubscription> BoothSubscriptions { get; set; }

        public virtual DbSet<Complaint> Complaints { get; set; }

        public virtual DbSet<ComplaintImage> ComplaintImages { get; set; }

        public virtual DbSet<Conversation> Conversations { get; set; }

        public virtual DbSet<FoodCategory> FoodCategories { get; set; }

        public virtual DbSet<FoodImage> FoodImages { get; set; }

        public virtual DbSet<FoodItem> FoodItems { get; set; }

        public virtual DbSet<FoodPrice> FoodPrices { get; set; }

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

        public virtual DbSet<PromotionUsage> PromotionUsages { get; set; }

        public virtual DbSet<Review> Reviews { get; set; }

        public virtual DbSet<ReviewReply> ReviewReplies { get; set; }

        public virtual DbSet<Role> Roles { get; set; }

        public virtual DbSet<User> Users { get; set; }

        public virtual DbSet<Zone> Zones { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Apply all configurations from the current assembly
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.Entity<Booth>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Booth_pkey");

                entity.ToTable("Booth", tb => tb.HasComment("Gian hàng ẩm thực - thực thể trung tâm, mỗi gian hàng thuộc 1 NightMarket và do 1 User (BoothOwner) quản lý"));

                entity.HasIndex(e => e.NightMarketId, "idx_booth_nightmarket");

                entity.HasIndex(e => e.BoothOwnerId, "idx_booth_owner");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.AverageRating)
                    .HasPrecision(3, 2)
                    .HasDefaultValueSql("0")
                    .HasComment("Cache điểm trung bình review, cập nhật qua trigger hoặc job định kỳ");
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
                    .HasComment("Pending: chờ Admin duyệt | Active: hoạt động | Inactive: tạm ngừng | Suspended: bị khóa do vi phạm");
                entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.BoothOwner).WithMany(p => p.Booths)
                    .HasForeignKey(d => d.BoothOwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Booth_BoothOwnerId_fkey");

                entity.HasOne(d => d.NightMarket).WithMany(p => p.Booths)
                    .HasForeignKey(d => d.NightMarketId)
                    .HasConstraintName("Booth_NightMarketId_fkey");
            });

            modelBuilder.Entity<BoothDocument>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("BoothDocuments_pkey");

                entity.ToTable(tb => tb.HasComment("Giấy tờ pháp lý của gian hàng để Admin xác minh trước khi cho phép hoạt động"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.DocumentType);
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

                entity.ToTable(tb => tb.HasComment("Thư viện ảnh (gallery) của gian hàng"));

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

                entity.ToTable(tb => tb.HasComment("Vị trí cụ thể (tọa độ) của 1 gian hàng trên 1 sơ đồ mặt bằng"));

                entity.HasIndex(e => e.BoothId, "BoothLocations_BoothId_key").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Xcoordinate)
                    .HasPrecision(10, 2)
                    .HasColumnName("XCoordinate");
                entity.Property(e => e.Ycoordinate)
                    .HasPrecision(10, 2)
                    .HasColumnName("YCoordinate");

                entity.HasOne(d => d.Booth).WithOne(p => p.BoothLocation)
                    .HasForeignKey<BoothLocation>(d => d.BoothId)
                    .HasConstraintName("BoothLocations_BoothId_fkey");

                entity.HasOne(d => d.Layout).WithMany(p => p.BoothLocations)
                    .HasForeignKey(d => d.LayoutId)
                    .HasConstraintName("BoothLocations_LayoutId_fkey");
            });

            modelBuilder.Entity<BoothPaymentInfo>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("BoothPaymentInfos_pkey");

                entity.ToTable(tb => tb.HasComment("Thông tin tài khoản/QR nhận thanh toán của gian hàng"));

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

                entity.ToTable(tb => tb.HasComment("Lịch sử đăng ký gói dịch vụ của gian hàng"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying")
                    .HasComment("Active | Expired | Cancelled");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.BoothSubscriptions)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("BoothSubscriptions_BoothId_fkey");

                entity.HasOne(d => d.Package).WithMany(p => p.BoothSubscriptions)
                    .HasForeignKey(d => d.PackageId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("BoothSubscriptions_PackageId_fkey");
            });

            modelBuilder.Entity<Complaint>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Complaints_pkey");

                entity.ToTable(tb => tb.HasComment("Khiếu nại của khách hàng về đơn hàng/gian hàng"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Open'::character varying")
                    .HasComment("Open | InProgress | Resolved | Rejected");
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

                entity.ToTable(tb => tb.HasComment("Ảnh minh chứng đính kèm theo khiếu nại"));

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

                entity.ToTable(tb => tb.HasComment("Cuộc trò chuyện giữa 1 khách hàng và 1 gian hàng - dùng SignalR để realtime"));

                entity.HasIndex(e => new { e.CustomerId, e.BoothOwnerId }, "uq_conversation_customer_boothowner").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.BoothOwner).WithMany()
                    .HasForeignKey(d => d.BoothOwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Conversations_BoothOwnerId_fkey");

                entity.HasOne(d => d.Customer).WithMany(p => p.Conversations)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Conversations_CustomerId_fkey");
            });

            modelBuilder.Entity<FoodCategory>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("FoodCategories_pkey");

                entity.ToTable(tb => tb.HasComment("Danh mục món ăn của từng gian hàng"));

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

                entity.ToTable(tb => tb.HasComment("Thư viện ảnh (gallery) cho từng món ăn"));

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

                entity.ToTable("FoodItem", tb => tb.HasComment("Món ăn của từng gian hàng"));

                entity.HasIndex(e => e.BoothId, "idx_fooditem_booth");

                entity.HasIndex(e => e.CategoryId, "idx_fooditem_category");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsAvailable)
                    .HasDefaultValue(true)
                    .HasComment("false khi món hết nguyên liệu hoặc chủ quán tạm ẩn");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.IsFeatured).HasDefaultValue(false);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Price)
                    .HasPrecision(12, 2)
                    .HasComment("Giá mặc định. Nếu có FoodPrice theo ngày hiện tại thì giá đó được ưu tiên (override)");
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

            modelBuilder.Entity<FoodPrice>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("FoodPrice_pkey");

                entity.ToTable("FoodPrice", tb => tb.HasComment("Bảng giá theo ngày trong tuần - override giá mặc định của FoodItem"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Price).HasPrecision(12, 2);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.FoodItem).WithMany(p => p.FoodPrices)
                    .HasForeignKey(d => d.FoodItemId)
                    .HasConstraintName("FoodPrice_FoodItemId_fkey");
            });

            modelBuilder.Entity<LayoutEdge>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("LayoutEdges_pkey");

                entity.ToTable(tb => tb.HasComment("Cạnh nối giữa 2 LayoutNode - thể hiện đường đi và khoảng cách, dùng cho thuật toán tìm đường ngắn nhất trong chợ"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Distance).HasPrecision(10, 2);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<LayoutNode>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("LayoutNodes_pkey");

                entity.ToTable(tb => tb.HasComment("Các điểm/nút (node) trên sơ đồ mặt bằng - là đỉnh của đồ thị dùng cho tìm đường nội bộ chợ"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.NodeName).HasMaxLength(100);
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
            });

            modelBuilder.Entity<MarketLayout>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("MarketLayouts_pkey");

                entity.ToTable(tb => tb.HasComment("Sơ đồ mặt bằng của một chợ đêm - dùng làm nền để đặt các điểm (LayoutNodes) và gian hàng (BoothLocations)"));

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

                entity.ToTable("Message", tb => tb.HasComment("Tin nhắn trong cuộc trò chuyện - truyền tải qua SignalR Hub"));

                entity.HasIndex(e => new { e.ConversationId, e.CreatedAt }, "idx_message_conversation");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                entity.Property(e => e.Type)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Text'::character varying")
                    .HasComment("Text | Image | System");
                entity.Property(e => e.SenderRole)
                    .HasMaxLength(20)
                    .HasComment("Snapshot vai trò người gửi: Customer | BoothOwner");
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

                entity.ToTable("NightMarket", tb => tb.HasComment("Thông tin các chợ đêm - đơn vị quản lý cấp cao nhất, chứa nhiều Booth"));

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
                entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
                entity.Property(e => e.TotalBooth)
                    .HasDefaultValue(0)
                    .HasComment("Số lượng gian hàng - giá trị cache, đồng bộ qua trigger hoặc job định kỳ");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Notification_pkey");

                entity.ToTable("Notification", tb => tb.HasComment("Thông báo đẩy (push notification qua FCM) cho người dùng"));

                entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_notification_user").IsDescending(false, true);

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.BoothId).HasComment("NULL khi thông báo không gắn với gian hàng cụ thể (VD: thông báo hệ thống)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.Type)
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .HasComment("NewOrder | OrderStatusChanged | NewMessage | Promotion | System");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.Notifications)
                    .HasForeignKey(d => d.BoothId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("Notification_BoothId_fkey");

                entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("Notification_UserId_fkey");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Order_pkey");

                entity.ToTable("Order", tb => tb.HasComment("Đơn hàng của khách"));

                entity.HasIndex(e => e.OrderCode, "Order_OrderCode_key").IsUnique();

                entity.HasIndex(e => e.CustomerId, "idx_order_customer");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.DiscountAmount).HasPrecision(12, 2);
                entity.Property(e => e.FinalAmount)
                    .HasPrecision(12, 2)
                    .HasComment("TotalAmount - DiscountAmount");
                entity.Property(e => e.OrderCode).HasMaxLength(50);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Pending'::character varying")
                    .HasComment("Pending | Confirmed | Preparing | Completed | Cancelled");
                entity.Property(e => e.TotalAmount).HasPrecision(12, 2);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Order_CustomerId_fkey");
            });

            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("OrderDetail_pkey");

                entity.ToTable("OrderDetail", tb => tb.HasComment("Chi tiết món ăn trong từng đơn hàng"));

                entity.HasIndex(e => e.OrderId, "idx_orderdetail_order");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.TotalPrice).HasPrecision(12, 2);
                entity.Property(e => e.UnitPrice)
                    .HasPrecision(12, 2)
                    .HasComment("SNAPSHOT giá tại thời điểm đặt hàng - KHÔNG tính lại từ FoodItem.Price");
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

                entity.ToTable(tb => tb.HasComment("Lịch sử giao dịch thanh toán/hoàn tiền - tích hợp đa cổng VNPay/ZaloPay/MoMo/Payos"));

                entity.HasIndex(e => e.OrderId, "idx_payments_order");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Amount).HasPrecision(12, 2);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Currency)
                    .HasMaxLength(10)
                    .HasDefaultValueSql("'VND'::character varying");
                entity.Property(e => e.Gateway).HasMaxLength(20);
                entity.Property(e => e.GatewayRef)
                    .HasMaxLength(255)
                    .HasComment("Mã tham chiếu từ cổng thanh toán bên thứ 3 - dùng để tra soát/khiếu nại");
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Pending'::character varying");
                entity.Property(e => e.Type)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasComment("Payment: thu tiền | Refund: hoàn tiền");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.BoothOwner).WithMany(p => p.Payments)
                    .HasForeignKey(d => d.BoothOwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("Payments_BoothOwnerId_fkey");

                entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                    .HasForeignKey(d => d.OrderId)
                    .HasConstraintName("Payments_OrderId_fkey");
            });

            modelBuilder.Entity<Package>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Package_pkey");

                entity.ToTable("Package");

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.PackageName).HasMaxLength(100);
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
                entity.Property(e => e.Price).HasPrecision(12, 2);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Package).WithMany(p => p.PackagePrices)
                    .HasForeignKey(d => d.PackageId)
                    .HasConstraintName("PackagePrice_PackageId_fkey");
            });

            modelBuilder.Entity<Promotion>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Promotion_pkey");

                entity.ToTable("Promotion", tb => tb.HasComment("Chương trình khuyến mãi/mã giảm giá do gian hàng tạo"));

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.DiscountType)
                    .HasMaxLength(20)
                    .HasComment("Percentage: giảm % | FixedAmount: giảm số tiền cố định");
                entity.Property(e => e.DiscountValue).HasPrecision(12, 2);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValueSql("'Active'::character varying");
                entity.Property(e => e.PromotionCode).HasMaxLength(50);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Booth).WithMany(p => p.Promotions)
                    .HasForeignKey(d => d.BoothId)
                    .HasConstraintName("Promotion_BoothId_fkey");
            });

            modelBuilder.Entity<PromotionUsage>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PromotionUsages_pkey");

                entity.ToTable(tb => tb.HasComment("Lịch sử sử dụng mã khuyến mãi - kiểm tra UsageLimit và chống dùng trùng"));

                entity.HasIndex(e => new { e.PromotionId, e.OrderId }, "uq_promotionusage_order").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
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

                entity.ToTable(tb => tb.HasComment("Đánh giá của khách hàng cho gian hàng, gắn liền với 1 đơn hàng đã hoàn tất"));

                entity.HasIndex(e => e.BoothId, "idx_reviews_booth");

                entity.HasIndex(e => e.OrderId, "uq_review_order").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.IsVisible)
                    .HasDefaultValue(true)
                    .HasComment("false: Admin ẩn review nhưng vẫn giữ dữ liệu để tính rating");
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

                entity.ToTable(tb => tb.HasComment("Phản hồi của chủ gian hàng đối với đánh giá - quan hệ 1-1 với Reviews"));

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

                entity.ToTable("Role", tb => tb.HasComment("Danh sách vai trò người dùng trong hệ thống (Customer, BoothOwner, Admin)"));

                entity.HasIndex(e => e.RoleName, "Role_RoleName_key").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.RoleName).HasMaxLength(50);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            });
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("User_pkey");

                entity.ToTable("User", tb => tb.HasComment("Tài khoản người dùng - dùng chung cho Customer, BoothOwner, Admin (phân biệt qua RoleId)"));

                entity.HasIndex(e => e.Email, "User_Email_key").IsUnique();

                entity.HasIndex(e => e.UserName, "User_UserName_key").IsUnique();

                entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
                entity.Property(e => e.Address).HasMaxLength(255);
                entity.Property(e => e.AvatarUrl).HasMaxLength(500);
                entity.Property(e => e.AuthProvider).HasMaxLength(30).HasDefaultValue("Local");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.Email).HasMaxLength(150);
                entity.Property(e => e.EmailVerificationTokenHash).HasMaxLength(64);
                entity.Property(e => e.FullName).HasMaxLength(150);
                entity.Property(e => e.GoogleId).HasMaxLength(100);
                entity.Property(e => e.PasswordHash)
                    .HasMaxLength(255)
                    .HasComment("Mật khẩu đã được mã hóa (hash), tuyệt đối không lưu plaintext");
                entity.Property(e => e.PasswordResetOtpHash).HasMaxLength(64);
                entity.Property(e => e.PasswordResetTokenHash).HasMaxLength(64);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.RefreshTokenHash).HasMaxLength(64);
                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasComment("Active: đang hoạt động | Inactive: chưa xác thực | Banned: bị khóa bởi Admin");
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
        }
    }
}
