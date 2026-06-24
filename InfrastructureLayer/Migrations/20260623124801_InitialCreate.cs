using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "FoodCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodCategories_pkey", x => x.Id);
                },
                comment: "Danh mục món ăn dùng chung toàn hệ thống");

            migrationBuilder.CreateTable(
                name: "LayoutEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    FromNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Distance = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("LayoutEdges_pkey", x => x.Id);
                },
                comment: "Cạnh nối giữa 2 LayoutNode - thể hiện đường đi và khoảng cách, dùng cho thuật toán tìm đường ngắn nhất trong chợ");

            migrationBuilder.CreateTable(
                name: "NightMarket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    OpeningHours = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ClosingHours = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TotalBooth = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Số lượng gian hàng - giá trị cache, đồng bộ qua trigger hoặc job định kỳ"),
                    MapWidth = table.Column<int>(type: "integer", nullable: true),
                    MapHeight = table.Column<int>(type: "integer", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("NightMarket_pkey", x => x.Id);
                },
                comment: "Thông tin các chợ đêm - đơn vị quản lý cấp cao nhất, chứa nhiều Booth");

            migrationBuilder.CreateTable(
                name: "Package",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageName = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Package", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    RoleName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Role_pkey", x => x.Id);
                },
                comment: "Danh sách vai trò người dùng trong hệ thống (Customer, BoothOwner, Admin)");

            migrationBuilder.CreateTable(
                name: "MarketLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    LayoutImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("MarketLayouts_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "MarketLayouts_NightMarketId_fkey",
                        column: x => x.NightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Sơ đồ mặt bằng của một chợ đêm - dùng làm nền để đặt các điểm (LayoutNodes) và gian hàng (BoothLocations)");

            migrationBuilder.CreateTable(
                name: "Zone",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zone", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zone_NightMarket_NightMarketId",
                        column: x => x.NightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackagePrice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagePrice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackagePrice_Package_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Package",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Mật khẩu đã được mã hóa (hash), tuyệt đối không lưu plaintext"),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DoB = table.Column<DateOnly>(type: "date", nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying", comment: "Active: đang hoạt động | Inactive: chưa xác thực | Banned: bị khóa bởi Admin"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("User_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "User_RoleId_fkey",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id");
                },
                comment: "Tài khoản người dùng - dùng chung cho Customer, BoothOwner, Admin (phân biệt qua RoleId)");

            migrationBuilder.CreateTable(
                name: "LayoutNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    LayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    XCoordinate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    YCoordinate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("LayoutNodes_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "LayoutNodes_LayoutId_fkey",
                        column: x => x.LayoutId,
                        principalTable: "MarketLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Các điểm/nút (node) trên sơ đồ mặt bằng - là đỉnh của đồ thị dùng cho tìm đường nội bộ chợ");

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Conversations_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Conversations_BoothOwnerId_fkey",
                        column: x => x.BoothOwnerId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "Conversations_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id");
                },
                comment: "Cuộc trò chuyện giữa 1 khách hàng và 1 gian hàng - dùng SignalR để realtime");

            migrationBuilder.CreateTable(
                name: "Order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Pending'::character varying", comment: "Pending | Confirmed | Preparing | Completed | Cancelled"),
                    PayStatus = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, comment: "TotalAmount - DiscountAmount"),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Order_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Order_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id");
                },
                comment: "Đơn hàng của khách");

            migrationBuilder.CreateTable(
                name: "BoothRegistration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedNightMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredLayoutNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoothName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoothRegistration", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoothRegistration_LayoutNodes_PreferredLayoutNodeId",
                        column: x => x.PreferredLayoutNodeId,
                        principalTable: "LayoutNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BoothRegistration_NightMarket_RequestedNightMarketId",
                        column: x => x.RequestedNightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoothRegistration_User_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoothRegistration_Zone_PreferredZoneId",
                        column: x => x.PreferredZoneId,
                        principalTable: "Zone",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Message",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Snapshot vai trò người gửi: Customer | BoothOwner"),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Text'::character varying", comment: "Text | Image | System"),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Message_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Message_ConversationId_fkey",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Message_SenderId_fkey",
                        column: x => x.SenderId,
                        principalTable: "User",
                        principalColumn: "Id");
                },
                comment: "Tin nhắn trong cuộc trò chuyện - truyền tải qua SignalR Hub");

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Payment: thu tiền | Refund: hoàn tiền"),
                    Gateway = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValueSql: "'VND'::character varying"),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Pending'::character varying"),
                    GatewayRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Mã tham chiếu từ cổng thanh toán bên thứ 3 - dùng để tra soát/khiếu nại"),
                    RefundReason = table.Column<string>(type: "text", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Payments_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Payments_BoothOwnerId_fkey",
                        column: x => x.BoothOwnerId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "Payments_OrderId_fkey",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Lịch sử giao dịch thanh toán/hoàn tiền - tích hợp đa cổng VNPay/ZaloPay/MoMo/Payos");

            migrationBuilder.CreateTable(
                name: "Booth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoothName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BoothCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SlotNumber = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MapPositionX = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MapPositionY = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    OpenTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    CloseTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    AverageRating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true, defaultValueSql: "0", comment: "Cache điểm trung bình review, cập nhật qua trigger hoặc job định kỳ"),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PackageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PackageExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentQRImage = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Pending'::character varying", comment: "Pending: chờ Admin duyệt | Active: hoạt động | Inactive: tạm ngừng | Suspended: bị khóa do vi phạm"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Booth_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Booth_BoothOwnerId_fkey",
                        column: x => x.BoothOwnerId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "Booth_NightMarketId_fkey",
                        column: x => x.NightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Booth_BoothRegistration_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "BoothRegistration",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Booth_Zone_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zone",
                        principalColumn: "Id");
                },
                comment: "Gian hàng ẩm thực - thực thể trung tâm, mỗi gian hàng thuộc 1 NightMarket và do 1 User (BoothOwner) quản lý");

            migrationBuilder.CreateTable(
                name: "BoothDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    RegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    DocumentUrl = table.Column<string>(type: "text", nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Pending'::character varying"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("BoothDocuments_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "BoothDocuments_BoothId_fkey",
                        column: x => x.RegistrationId,
                        principalTable: "BoothRegistration",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoothDocuments_Booth_BoothId",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id");
                },
                comment: "Giấy tờ pháp lý của gian hàng để Admin xác minh trước khi cho phép hoạt động");

            migrationBuilder.CreateTable(
                name: "BoothImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("BoothImages_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "BoothImages_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Thư viện ảnh (gallery) của gian hàng");

            migrationBuilder.CreateTable(
                name: "BoothLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    LayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    XCoordinate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    YCoordinate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("BoothLocations_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "BoothLocations_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "BoothLocations_LayoutId_fkey",
                        column: x => x.LayoutId,
                        principalTable: "MarketLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Vị trí cụ thể (tọa độ) của 1 gian hàng trên 1 sơ đồ mặt bằng");

            migrationBuilder.CreateTable(
                name: "BoothPaymentInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentType = table.Column<int>(type: "integer", maxLength: 20, nullable: false, comment: "BankTransfer | VNPay | MoMo | ZaloPay | Payos"),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankAccountHolder = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    QRImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Status = table.Column<int>(type: "integer", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("BoothPaymentInfos_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "BoothPaymentInfos_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Thông tin tài khoản/QR nhận thanh toán của gian hàng");

            migrationBuilder.CreateTable(
                name: "BoothSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying", comment: "Active | Expired | Cancelled"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("BoothSubscriptions_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "BoothSubscriptions_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "BoothSubscriptions_PackageId_fkey",
                        column: x => x.PackageId,
                        principalTable: "Package",
                        principalColumn: "Id");
                },
                comment: "Lịch sử đăng ký gói dịch vụ của gian hàng");

            migrationBuilder.CreateTable(
                name: "Complaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AdminResponse = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Open'::character varying", comment: "Open | InProgress | Resolved | Rejected"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Complaints_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Complaints_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "Complaints_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "Complaints_OrderId_fkey",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id");
                },
                comment: "Khiếu nại của khách hàng về đơn hàng/gian hàng");

            migrationBuilder.CreateTable(
                name: "FoodItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, comment: "Giá mặc định. Nếu có FoodPrice theo ngày hiện tại thì giá đó được ưu tiên (override)"),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "false khi món hết nguyên liệu hoặc chủ quán tạm ẩn"),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodItem_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FoodItem_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FoodItem_CategoryId_fkey",
                        column: x => x.CategoryId,
                        principalTable: "FoodCategories",
                        principalColumn: "Id");
                },
                comment: "Món ăn của từng gian hàng");

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: true, comment: "NULL khi thông báo không gắn với gian hàng cụ thể (VD: thông báo hệ thống)"),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "NewOrder | OrderStatusChanged | NewMessage | Promotion | System"),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Notification_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Notification_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Notification_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Thông báo đẩy (push notification qua FCM) cho người dùng");

            migrationBuilder.CreateTable(
                name: "Promotion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Percentage: giảm % | FixedAmount: giảm số tiền cố định"),
                    DiscountValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Promotion_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Promotion_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Chương trình khuyến mãi/mã giảm giá do gian hàng tạo");

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<short>(type: "smallint", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "false: Admin ẩn review nhưng vẫn giữ dữ liệu để tính rating"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Reviews_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Reviews_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Reviews_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "Reviews_OrderId_fkey",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id");
                },
                comment: "Đánh giá của khách hàng cho gian hàng, gắn liền với 1 đơn hàng đã hoàn tất");

            migrationBuilder.CreateTable(
                name: "ComplaintImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ComplaintId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ComplaintImages_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "ComplaintImages_ComplaintId_fkey",
                        column: x => x.ComplaintId,
                        principalTable: "Complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Ảnh minh chứng đính kèm theo khiếu nại");

            migrationBuilder.CreateTable(
                name: "FoodImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodImages_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FoodImages_FoodItemId_fkey",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Thư viện ảnh (gallery) cho từng món ăn");

            migrationBuilder.CreateTable(
                name: "FoodPrice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodPrice_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "FoodPrice_FoodItemId_fkey",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Bảng giá theo ngày trong tuần - override giá mặc định của FoodItem");

            migrationBuilder.CreateTable(
                name: "OrderDetail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, comment: "SNAPSHOT giá tại thời điểm đặt hàng - KHÔNG tính lại từ FoodItem.Price"),
                    TotalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("OrderDetail_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "OrderDetail_FoodItemId_fkey",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "OrderDetail_OrderId_fkey",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Chi tiết món ăn trong từng đơn hàng");

            migrationBuilder.CreateTable(
                name: "PromotionUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PromotionUsages_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "PromotionUsages_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "PromotionUsages_OrderId_fkey",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "PromotionUsages_PromotionId_fkey",
                        column: x => x.PromotionId,
                        principalTable: "Promotion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Lịch sử sử dụng mã khuyến mãi - kiểm tra UsageLimit và chống dùng trùng");

            migrationBuilder.CreateTable(
                name: "ReviewReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ReviewReplies_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "ReviewReplies_BoothOwnerId_fkey",
                        column: x => x.BoothOwnerId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "ReviewReplies_ReviewId_fkey",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Phản hồi của chủ gian hàng đối với đánh giá - quan hệ 1-1 với Reviews");

            migrationBuilder.CreateIndex(
                name: "idx_booth_nightmarket",
                table: "Booth",
                column: "NightMarketId");

            migrationBuilder.CreateIndex(
                name: "idx_booth_owner",
                table: "Booth",
                column: "BoothOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booth_ZoneId",
                table: "Booth",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothDocuments_BoothId",
                table: "BoothDocuments",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothDocuments_RegistrationId",
                table: "BoothDocuments",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothImages_BoothId",
                table: "BoothImages",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "BoothLocations_BoothId_key",
                table: "BoothLocations",
                column: "BoothId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoothLocations_LayoutId",
                table: "BoothLocations",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothPaymentInfos_BoothId",
                table: "BoothPaymentInfos",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistration_OwnerId",
                table: "BoothRegistration",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistration_PreferredLayoutNodeId",
                table: "BoothRegistration",
                column: "PreferredLayoutNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistration_PreferredZoneId",
                table: "BoothRegistration",
                column: "PreferredZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistration_RequestedNightMarketId",
                table: "BoothRegistration",
                column: "RequestedNightMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothSubscriptions_BoothId",
                table: "BoothSubscriptions",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothSubscriptions_PackageId",
                table: "BoothSubscriptions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintImages_ComplaintId",
                table: "ComplaintImages",
                column: "ComplaintId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_BoothId",
                table: "Complaints",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_CustomerId",
                table: "Complaints",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_OrderId",
                table: "Complaints",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_BoothOwnerId",
                table: "Conversations",
                column: "BoothOwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_conversation_customer_boothowner",
                table: "Conversations",
                columns: new[] { "CustomerId", "BoothOwnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "FoodCategories_Name_key",
                table: "FoodCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoodImages_FoodItemId",
                table: "FoodImages",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "idx_fooditem_booth",
                table: "FoodItem",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "idx_fooditem_category",
                table: "FoodItem",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodPrice_FoodItemId",
                table: "FoodPrice",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutNodes_LayoutId",
                table: "LayoutNodes",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketLayouts_NightMarketId",
                table: "MarketLayouts",
                column: "NightMarketId");

            migrationBuilder.CreateIndex(
                name: "idx_message_conversation",
                table: "Message",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Message_SenderId",
                table: "Message",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "idx_notification_user",
                table: "Notification",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_BoothId",
                table: "Notification",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "idx_order_customer",
                table: "Order",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "Order_OrderCode_key",
                table: "Order",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_orderdetail_order",
                table: "OrderDetail",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetail_FoodItemId",
                table: "OrderDetail",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePrice_PackageId",
                table: "PackagePrice",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "idx_payments_order",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BoothOwnerId",
                table: "Payments",
                column: "BoothOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotion_BoothId",
                table: "Promotion",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionUsages_CustomerId",
                table: "PromotionUsages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionUsages_OrderId",
                table: "PromotionUsages",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "uq_promotionusage_order",
                table: "PromotionUsages",
                columns: new[] { "PromotionId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReplies_BoothOwnerId",
                table: "ReviewReplies",
                column: "BoothOwnerId");

            migrationBuilder.CreateIndex(
                name: "ReviewReplies_ReviewId_key",
                table: "ReviewReplies",
                column: "ReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_reviews_booth",
                table: "Reviews",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CustomerId",
                table: "Reviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "uq_review_order",
                table: "Reviews",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Role_RoleName_key",
                table: "Role",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_RoleId",
                table: "User",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "User_Email_key",
                table: "User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "User_UserName_key",
                table: "User",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Zone_NightMarketId",
                table: "Zone",
                column: "NightMarketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoothDocuments");

            migrationBuilder.DropTable(
                name: "BoothImages");

            migrationBuilder.DropTable(
                name: "BoothLocations");

            migrationBuilder.DropTable(
                name: "BoothPaymentInfos");

            migrationBuilder.DropTable(
                name: "BoothSubscriptions");

            migrationBuilder.DropTable(
                name: "ComplaintImages");

            migrationBuilder.DropTable(
                name: "FoodImages");

            migrationBuilder.DropTable(
                name: "FoodPrice");

            migrationBuilder.DropTable(
                name: "LayoutEdges");

            migrationBuilder.DropTable(
                name: "Message");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "OrderDetail");

            migrationBuilder.DropTable(
                name: "PackagePrice");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PromotionUsages");

            migrationBuilder.DropTable(
                name: "ReviewReplies");

            migrationBuilder.DropTable(
                name: "Complaints");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "FoodItem");

            migrationBuilder.DropTable(
                name: "Package");

            migrationBuilder.DropTable(
                name: "Promotion");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "FoodCategories");

            migrationBuilder.DropTable(
                name: "Booth");

            migrationBuilder.DropTable(
                name: "Order");

            migrationBuilder.DropTable(
                name: "BoothRegistration");

            migrationBuilder.DropTable(
                name: "LayoutNodes");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Zone");

            migrationBuilder.DropTable(
                name: "MarketLayouts");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "NightMarket");
        }
    }
}
