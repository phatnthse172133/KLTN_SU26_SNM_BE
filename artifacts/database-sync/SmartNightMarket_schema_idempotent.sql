CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "LayoutEdges" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "FromNodeId" uuid NOT NULL,
        "ToNodeId" uuid NOT NULL,
        "Distance" numeric(10,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "LayoutEdges_pkey" PRIMARY KEY ("Id")
    );
    COMMENT ON TABLE "LayoutEdges" IS 'Cạnh nối giữa 2 LayoutNode - thể hiện đường đi và khoảng cách, dùng cho thuật toán tìm đường ngắn nhất trong chợ';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "NightMarket" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "Name" character varying(200) NOT NULL,
        "Description" text,
        "Address" character varying(255) NOT NULL,
        "Latitude" numeric(10,7),
        "Longitude" numeric(10,7),
        "OpeningHours" time without time zone,
        "ClosingHours" time without time zone,
        "TotalBooth" integer NOT NULL DEFAULT 0,
        "MapWidth" integer,
        "MapHeight" integer,
        "ThumbnailUrl" character varying(500),
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "NightMarket_pkey" PRIMARY KEY ("Id")
    );
    COMMENT ON TABLE "NightMarket" IS 'Thông tin các chợ đêm - đơn vị quản lý cấp cao nhất, chứa nhiều Booth';
    COMMENT ON COLUMN "NightMarket"."TotalBooth" IS 'Số lượng gian hàng - giá trị cache, đồng bộ qua trigger hoặc job định kỳ';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Package" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "PackageName" character varying(100) NOT NULL,
        "Price" numeric(12,2) NOT NULL,
        "DurationDays" integer NOT NULL,
        "Description" text,
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Package_pkey" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Role" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "RoleName" character varying(50) NOT NULL,
        "Description" text,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Role_pkey" PRIMARY KEY ("Id")
    );
    COMMENT ON TABLE "Role" IS 'Danh sách vai trò người dùng trong hệ thống (Customer, BoothOwner, Admin)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "MarketLayouts" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "NightMarketId" uuid NOT NULL,
        "LayoutImageUrl" character varying(500),
        "Width" integer NOT NULL,
        "Height" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "MarketLayouts_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "MarketLayouts_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES "NightMarket" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "MarketLayouts" IS 'Sơ đồ mặt bằng của một chợ đêm - dùng làm nền để đặt các điểm (LayoutNodes) và gian hàng (BoothLocations)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Zones" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "NightMarketId" uuid NOT NULL,
        "ZoneName" character varying(100) NOT NULL,
        "Description" text,
        "Color" character varying(50),
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Zones_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Zones_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES "NightMarket" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "PackagePrice" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "PackageId" uuid NOT NULL,
        "Price" numeric(12,2) NOT NULL,
        "StartDate" timestamp with time zone,
        "EndDate" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PackagePrice_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "PackagePrice_PackageId_fkey" FOREIGN KEY ("PackageId") REFERENCES "Package" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "User" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "RoleId" uuid NOT NULL,
        "UserName" character varying(100) NOT NULL,
        "PasswordHash" character varying(255) NOT NULL,
        "FullName" character varying(150) NOT NULL,
        "Email" character varying(150) NOT NULL,
        "Phone" character varying(20),
        "Address" character varying(255),
        "DoB" date,
        "AvatarUrl" character varying(500),
        "AuthProvider" character varying(30) NOT NULL DEFAULT 'Local',
        "GoogleId" character varying(100),
        "RefreshTokenHash" character varying(64),
        "RefreshTokenExpiresAt" timestamp with time zone,
        "EmailVerificationTokenHash" character varying(64),
        "EmailVerificationTokenExpiresAt" timestamp with time zone,
        "PasswordResetOtpHash" character varying(64),
        "PasswordResetOtpExpiresAt" timestamp with time zone,
        "PasswordResetTokenHash" character varying(64),
        "PasswordResetTokenExpiresAt" timestamp with time zone,
        "Status" character varying(20) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "User_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "User_RoleId_fkey" FOREIGN KEY ("RoleId") REFERENCES "Role" ("Id")
    );
    COMMENT ON TABLE "User" IS 'Tài khoản người dùng - dùng chung cho Customer, BoothOwner, Admin (phân biệt qua RoleId)';
    COMMENT ON COLUMN "User"."PasswordHash" IS 'Mật khẩu đã được mã hóa (hash), tuyệt đối không lưu plaintext';
    COMMENT ON COLUMN "User"."Status" IS 'Active: đang hoạt động | Inactive: chưa xác thực | Banned: bị khóa bởi Admin';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "LayoutNodes" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "LayoutId" uuid NOT NULL,
        "NodeName" character varying(100),
        "XCoordinate" numeric(10,2) NOT NULL,
        "YCoordinate" numeric(10,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "LayoutNodes_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "LayoutNodes_LayoutId_fkey" FOREIGN KEY ("LayoutId") REFERENCES "MarketLayouts" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "LayoutNodes" IS 'Các điểm/nút (node) trên sơ đồ mặt bằng - là đỉnh của đồ thị dùng cho tìm đường nội bộ chợ';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Conversations" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "CustomerId" uuid NOT NULL,
        "BoothOwnerId" uuid NOT NULL,
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Conversations_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Conversations_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES "User" ("Id"),
        CONSTRAINT "Conversations_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES "User" ("Id")
    );
    COMMENT ON TABLE "Conversations" IS 'Cuộc trò chuyện giữa 1 khách hàng và 1 gian hàng - dùng SignalR để realtime';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Order" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "CustomerId" uuid NOT NULL,
        "OrderCode" character varying(50) NOT NULL,
        "Status" character varying(20) NOT NULL DEFAULT ('Pending'::character varying),
        "PayStatus" integer NOT NULL,
        "TotalAmount" numeric(12,2) NOT NULL,
        "DiscountAmount" numeric(12,2) NOT NULL,
        "FinalAmount" numeric(12,2) NOT NULL,
        "Note" text,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Order_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Order_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES "User" ("Id")
    );
    COMMENT ON TABLE "Order" IS 'Đơn hàng của khách';
    COMMENT ON COLUMN "Order"."Status" IS 'Pending | Confirmed | Preparing | Completed | Cancelled';
    COMMENT ON COLUMN "Order"."FinalAmount" IS 'TotalAmount - DiscountAmount';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "BoothRegistrations" (
        "Id" uuid NOT NULL,
        "OwnerId" uuid NOT NULL,
        "RequestedNightMarketId" uuid NOT NULL,
        "PreferredZoneId" uuid,
        "PreferredLayoutNodeId" uuid,
        "BoothName" text NOT NULL,
        "Description" text,
        "Phone" text,
        "Status" integer NOT NULL,
        "RejectReason" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_BoothRegistrations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_BoothRegistrations_LayoutNodes_PreferredLayoutNodeId" FOREIGN KEY ("PreferredLayoutNodeId") REFERENCES "LayoutNodes" ("Id"),
        CONSTRAINT "FK_BoothRegistrations_NightMarket_RequestedNightMarketId" FOREIGN KEY ("RequestedNightMarketId") REFERENCES "NightMarket" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_BoothRegistrations_User_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "User" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_BoothRegistrations_Zones_PreferredZoneId" FOREIGN KEY ("PreferredZoneId") REFERENCES "Zones" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Message" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "ConversationId" uuid NOT NULL,
        "SenderId" uuid NOT NULL,
        "SenderRole" character varying(20) NOT NULL,
        "Type" character varying(20) NOT NULL DEFAULT ('Text'::character varying),
        "Content" text NOT NULL,
        "IsRead" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Message_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Message_ConversationId_fkey" FOREIGN KEY ("ConversationId") REFERENCES "Conversations" ("Id") ON DELETE CASCADE,
        CONSTRAINT "Message_SenderId_fkey" FOREIGN KEY ("SenderId") REFERENCES "User" ("Id")
    );
    COMMENT ON TABLE "Message" IS 'Tin nhắn trong cuộc trò chuyện - truyền tải qua SignalR Hub';
    COMMENT ON COLUMN "Message"."SenderRole" IS 'Snapshot vai trò người gửi: Customer | BoothOwner';
    COMMENT ON COLUMN "Message"."Type" IS 'Text | Image | System';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Payments" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "OrderId" uuid NOT NULL,
        "BoothOwnerId" uuid NOT NULL,
        "Type" character varying(20) NOT NULL,
        "Gateway" character varying(20) NOT NULL,
        "Amount" numeric(12,2) NOT NULL,
        "Currency" character varying(10) NOT NULL DEFAULT ('VND'::character varying),
        "Status" character varying(20) NOT NULL DEFAULT ('Pending'::character varying),
        "GatewayRef" character varying(255),
        "RefundReason" text,
        "PaidAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Payments_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Payments_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES "User" ("Id"),
        CONSTRAINT "Payments_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES "Order" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "Payments" IS 'Lịch sử giao dịch thanh toán/hoàn tiền - tích hợp đa cổng VNPay/ZaloPay/MoMo/Payos';
    COMMENT ON COLUMN "Payments"."Type" IS 'Payment: thu tiền | Refund: hoàn tiền';
    COMMENT ON COLUMN "Payments"."GatewayRef" IS 'Mã tham chiếu từ cổng thanh toán bên thứ 3 - dùng để tra soát/khiếu nại';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Booth" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "RegistrationId" uuid NOT NULL,
        "NightMarketId" uuid NOT NULL,
        "BoothOwnerId" uuid NOT NULL,
        "ZoneId" uuid,
        "BoothName" character varying(200) NOT NULL,
        "BoothCode" character varying(50),
        "Description" text,
        "PhoneNumber" character varying(20),
        "SlotNumber" text,
        "ThumbnailUrl" character varying(500),
        "MapPositionX" numeric(10,2),
        "MapPositionY" numeric(10,2),
        "Latitude" numeric(10,7),
        "Longitude" numeric(10,7),
        "OpenTime" time without time zone,
        "CloseTime" time without time zone,
        "AverageRating" numeric(3,2) DEFAULT (0),
        "IsFeatured" boolean NOT NULL DEFAULT FALSE,
        "PackageName" character varying(100),
        "PackageExpiryDate" timestamp with time zone,
        "PaymentQRImage" text,
        "Status" character varying(20) NOT NULL DEFAULT ('Pending'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Booth_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Booth_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES "User" ("Id"),
        CONSTRAINT "Booth_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES "NightMarket" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Booth_BoothRegistrations_RegistrationId" FOREIGN KEY ("RegistrationId") REFERENCES "BoothRegistrations" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Booth_Zones_ZoneId" FOREIGN KEY ("ZoneId") REFERENCES "Zones" ("Id")
    );
    COMMENT ON TABLE "Booth" IS 'Gian hàng ẩm thực - thực thể trung tâm, mỗi gian hàng thuộc 1 NightMarket và do 1 User (BoothOwner) quản lý';
    COMMENT ON COLUMN "Booth"."AverageRating" IS 'Cache điểm trung bình review, cập nhật qua trigger hoặc job định kỳ';
    COMMENT ON COLUMN "Booth"."Status" IS 'Pending: chờ Admin duyệt | Active: hoạt động | Inactive: tạm ngừng | Suspended: bị khóa do vi phạm';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "BoothDocuments" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "RegistrationId" uuid NOT NULL,
        "DocumentType" text NOT NULL,
        "DocumentUrl" text NOT NULL,
        "FileUrl" character varying(500) NOT NULL,
        "VerificationStatus" character varying(20) NOT NULL DEFAULT ('Pending'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "BoothId" uuid,
        CONSTRAINT "BoothDocuments_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "BoothDocuments_BoothId_fkey" FOREIGN KEY ("RegistrationId") REFERENCES "BoothRegistrations" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_BoothDocuments_Booth_BoothId" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id")
    );
    COMMENT ON TABLE "BoothDocuments" IS 'Giấy tờ pháp lý của gian hàng để Admin xác minh trước khi cho phép hoạt động';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "BoothImages" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid NOT NULL,
        "ImageUrl" character varying(500) NOT NULL,
        "DisplayOrder" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "BoothImages_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "BoothImages_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "BoothImages" IS 'Thư viện ảnh (gallery) của gian hàng';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "BoothLocations" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid NOT NULL,
        "LayoutId" uuid NOT NULL,
        "XCoordinate" numeric(10,2) NOT NULL,
        "YCoordinate" numeric(10,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "BoothLocations_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "BoothLocations_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE,
        CONSTRAINT "BoothLocations_LayoutId_fkey" FOREIGN KEY ("LayoutId") REFERENCES "MarketLayouts" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "BoothLocations" IS 'Vị trí cụ thể (tọa độ) của 1 gian hàng trên 1 sơ đồ mặt bằng';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "BoothPaymentInfos" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid NOT NULL,
        "PaymentType" integer NOT NULL,
        "BankName" character varying(100),
        "BankAccountNumber" character varying(50),
        "BankAccountHolder" character varying(150),
        "QRImageUrl" character varying(500),
        "IsDefault" boolean NOT NULL DEFAULT FALSE,
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "BoothPaymentInfos_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "BoothPaymentInfos_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "BoothPaymentInfos" IS 'Thông tin tài khoản/QR nhận thanh toán của gian hàng';
    COMMENT ON COLUMN "BoothPaymentInfos"."PaymentType" IS 'BankTransfer | VNPay | MoMo | ZaloPay | Payos';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "BoothSubscriptions" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid NOT NULL,
        "PackageId" uuid NOT NULL,
        "StartDate" timestamp with time zone NOT NULL,
        "EndDate" timestamp with time zone NOT NULL,
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "BoothSubscriptions_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "BoothSubscriptions_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE,
        CONSTRAINT "BoothSubscriptions_PackageId_fkey" FOREIGN KEY ("PackageId") REFERENCES "Package" ("Id")
    );
    COMMENT ON TABLE "BoothSubscriptions" IS 'Lịch sử đăng ký gói dịch vụ của gian hàng';
    COMMENT ON COLUMN "BoothSubscriptions"."Status" IS 'Active | Expired | Cancelled';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Complaints" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "CustomerId" uuid NOT NULL,
        "BoothId" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Description" text NOT NULL,
        "AdminResponse" text,
        "Status" character varying(20) NOT NULL DEFAULT ('Open'::character varying),
        "ResolutionAction" character varying(30),
        "PolicyViolation" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Complaints_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Complaints_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id"),
        CONSTRAINT "Complaints_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES "User" ("Id"),
        CONSTRAINT "Complaints_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES "Order" ("Id")
    );
    COMMENT ON TABLE "Complaints" IS 'Khiếu nại của khách hàng về đơn hàng/gian hàng';
    COMMENT ON COLUMN "Complaints"."Status" IS 'Open | InProgress | Resolved | Rejected';
    COMMENT ON COLUMN "Complaints"."ResolutionAction" IS 'NoViolation | Warning | SuspendBooth | CloseBooth';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "FoodCategories" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Description" text,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "FoodCategories_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "FoodCategories_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "FoodCategories" IS 'Danh mục món ăn của từng gian hàng';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Notification" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "UserId" uuid NOT NULL,
        "BoothId" uuid,
        "Type" character varying(50) NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Content" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Notification_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Notification_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE,
        CONSTRAINT "Notification_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "Notification" IS 'Thông báo đẩy (push notification qua FCM) cho người dùng';
    COMMENT ON COLUMN "Notification"."BoothId" IS 'NULL khi thông báo không gắn với gian hàng cụ thể (VD: thông báo hệ thống)';
    COMMENT ON COLUMN "Notification"."Type" IS 'NewOrder | OrderStatusChanged | NewMessage | Promotion | System';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Promotion" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid NOT NULL,
        "PromotionCode" character varying(50),
        "Title" character varying(200) NOT NULL,
        "Description" text,
        "DiscountType" character varying(20) NOT NULL,
        "DiscountValue" numeric(12,2) NOT NULL,
        "StartDate" timestamp with time zone NOT NULL,
        "EndDate" timestamp with time zone NOT NULL,
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Promotion_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Promotion_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "Promotion" IS 'Chương trình khuyến mãi/mã giảm giá do gian hàng tạo';
    COMMENT ON COLUMN "Promotion"."DiscountType" IS 'Percentage: giảm % | FixedAmount: giảm số tiền cố định';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "Reviews" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "Rating" smallint NOT NULL,
        "Content" text,
        "ImageUrl" character varying(500),
        "IsVisible" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Reviews_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Reviews_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE,
        CONSTRAINT "Reviews_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES "User" ("Id"),
        CONSTRAINT "Reviews_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES "Order" ("Id")
    );
    COMMENT ON TABLE "Reviews" IS 'Đánh giá của khách hàng cho gian hàng, gắn liền với 1 đơn hàng đã hoàn tất';
    COMMENT ON COLUMN "Reviews"."IsVisible" IS 'false: Admin ẩn review nhưng vẫn giữ dữ liệu để tính rating';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "ComplaintImages" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "ComplaintId" uuid NOT NULL,
        "ImageUrl" character varying(500) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "ComplaintImages_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "ComplaintImages_ComplaintId_fkey" FOREIGN KEY ("ComplaintId") REFERENCES "Complaints" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "ComplaintImages" IS 'Ảnh minh chứng đính kèm theo khiếu nại';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "FoodItem" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid NOT NULL,
        "CategoryId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" text,
        "Price" numeric(12,2) NOT NULL,
        "ThumbnailUrl" character varying(500),
        "IsAvailable" boolean NOT NULL DEFAULT TRUE,
        "IsFeatured" boolean NOT NULL DEFAULT FALSE,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "FoodItem_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "FoodItem_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FoodItem_CategoryId_fkey" FOREIGN KEY ("CategoryId") REFERENCES "FoodCategories" ("Id")
    );
    COMMENT ON TABLE "FoodItem" IS 'Món ăn của từng gian hàng';
    COMMENT ON COLUMN "FoodItem"."Price" IS 'Giá mặc định. Nếu có FoodPrice theo ngày hiện tại thì giá đó được ưu tiên (override)';
    COMMENT ON COLUMN "FoodItem"."IsAvailable" IS 'false khi món hết nguyên liệu hoặc chủ quán tạm ẩn';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "PromotionUsages" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "PromotionId" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PromotionUsages_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "PromotionUsages_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES "User" ("Id"),
        CONSTRAINT "PromotionUsages_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES "Order" ("Id") ON DELETE CASCADE,
        CONSTRAINT "PromotionUsages_PromotionId_fkey" FOREIGN KEY ("PromotionId") REFERENCES "Promotion" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "PromotionUsages" IS 'Lịch sử sử dụng mã khuyến mãi - kiểm tra UsageLimit và chống dùng trùng';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "ReviewReplies" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "ReviewId" uuid NOT NULL,
        "BoothOwnerId" uuid NOT NULL,
        "Content" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "ReviewReplies_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "ReviewReplies_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES "User" ("Id"),
        CONSTRAINT "ReviewReplies_ReviewId_fkey" FOREIGN KEY ("ReviewId") REFERENCES "Reviews" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "ReviewReplies" IS 'Phản hồi của chủ gian hàng đối với đánh giá - quan hệ 1-1 với Reviews';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "FoodImages" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "FoodItemId" uuid NOT NULL,
        "ImageUrl" character varying(500) NOT NULL,
        "DisplayOrder" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "FoodImages_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "FoodImages_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES "FoodItem" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "FoodImages" IS 'Thư viện ảnh (gallery) cho từng món ăn';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "FoodPrice" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "FoodItemId" uuid NOT NULL,
        "Price" numeric(12,2) NOT NULL,
        "StartDate" timestamp with time zone,
        "EndDate" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "FoodPrice_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "FoodPrice_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES "FoodItem" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "FoodPrice" IS 'Bảng giá theo ngày trong tuần - override giá mặc định của FoodItem';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE TABLE "OrderDetail" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "OrderId" uuid NOT NULL,
        "FoodItemId" uuid NOT NULL,
        "Quantity" integer NOT NULL,
        "UnitPrice" numeric(12,2) NOT NULL,
        "TotalPrice" numeric(12,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "OrderDetail_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "OrderDetail_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES "FoodItem" ("Id"),
        CONSTRAINT "OrderDetail_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES "Order" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "OrderDetail" IS 'Chi tiết món ăn trong từng đơn hàng';
    COMMENT ON COLUMN "OrderDetail"."UnitPrice" IS 'SNAPSHOT giá tại thời điểm đặt hàng - KHÔNG tính lại từ FoodItem.Price';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_booth_nightmarket ON "Booth" ("NightMarketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_booth_owner ON "Booth" ("BoothOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Booth_RegistrationId" ON "Booth" ("RegistrationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Booth_ZoneId" ON "Booth" ("ZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothDocuments_BoothId" ON "BoothDocuments" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothDocuments_RegistrationId" ON "BoothDocuments" ("RegistrationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothImages_BoothId" ON "BoothImages" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "BoothLocations_BoothId_key" ON "BoothLocations" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothLocations_LayoutId" ON "BoothLocations" ("LayoutId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothPaymentInfos_BoothId" ON "BoothPaymentInfos" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothRegistrations_OwnerId" ON "BoothRegistrations" ("OwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothRegistrations_PreferredLayoutNodeId" ON "BoothRegistrations" ("PreferredLayoutNodeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothRegistrations_PreferredZoneId" ON "BoothRegistrations" ("PreferredZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothRegistrations_RequestedNightMarketId" ON "BoothRegistrations" ("RequestedNightMarketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothSubscriptions_BoothId" ON "BoothSubscriptions" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_BoothSubscriptions_PackageId" ON "BoothSubscriptions" ("PackageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_ComplaintImages_ComplaintId" ON "ComplaintImages" ("ComplaintId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Complaints_BoothId" ON "Complaints" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Complaints_CustomerId" ON "Complaints" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Complaints_OrderId" ON "Complaints" ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Conversations_BoothOwnerId" ON "Conversations" ("BoothOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX uq_conversation_customer_boothowner ON "Conversations" ("CustomerId", "BoothOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "FoodCategories_BoothId_Name_key" ON "FoodCategories" ("BoothId", "Name") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_foodcategory_booth ON "FoodCategories" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_FoodImages_FoodItemId" ON "FoodImages" ("FoodItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_fooditem_booth ON "FoodItem" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_fooditem_category ON "FoodItem" ("CategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_FoodPrice_FoodItemId" ON "FoodPrice" ("FoodItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_LayoutNodes_LayoutId" ON "LayoutNodes" ("LayoutId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_MarketLayouts_NightMarketId" ON "MarketLayouts" ("NightMarketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_message_conversation ON "Message" ("ConversationId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Message_SenderId" ON "Message" ("SenderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_notification_user ON "Notification" ("UserId", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Notification_BoothId" ON "Notification" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_order_customer ON "Order" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "Order_OrderCode_key" ON "Order" ("OrderCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_orderdetail_order ON "OrderDetail" ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_OrderDetail_FoodItemId" ON "OrderDetail" ("FoodItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_PackagePrice_PackageId" ON "PackagePrice" ("PackageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_payments_order ON "Payments" ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Payments_BoothOwnerId" ON "Payments" ("BoothOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Promotion_BoothId" ON "Promotion" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_PromotionUsages_CustomerId" ON "PromotionUsages" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_PromotionUsages_OrderId" ON "PromotionUsages" ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX uq_promotionusage_order ON "PromotionUsages" ("PromotionId", "OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_ReviewReplies_BoothOwnerId" ON "ReviewReplies" ("BoothOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "ReviewReplies_ReviewId_key" ON "ReviewReplies" ("ReviewId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX idx_reviews_booth ON "Reviews" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Reviews_CustomerId" ON "Reviews" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX uq_review_order ON "Reviews" ("OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "Role_RoleName_key" ON "Role" ("RoleName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_User_GoogleId" ON "User" ("GoogleId") WHERE "GoogleId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_User_RefreshTokenHash" ON "User" ("RefreshTokenHash") WHERE "RefreshTokenHash" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_User_RoleId" ON "User" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "User_Email_key" ON "User" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE UNIQUE INDEX "User_UserName_key" ON "User" ("UserName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    CREATE INDEX "IX_Zones_NightMarketId" ON "Zones" ("NightMarketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260627095421_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260627095421_InitialCreate', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629131808_AddNightMarketManagementFields') THEN
    ALTER TABLE "NightMarket" RENAME COLUMN "MapWidth" TO "BoundaryWidthMeters";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629131808_AddNightMarketManagementFields') THEN
    ALTER TABLE "NightMarket" RENAME COLUMN "MapHeight" TO "BoundaryHeightMeters";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629131808_AddNightMarketManagementFields') THEN
    ALTER TABLE "NightMarket" ALTER COLUMN "Address" TYPE character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629131808_AddNightMarketManagementFields') THEN
    ALTER TABLE "NightMarket" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629131808_AddNightMarketManagementFields') THEN
    ALTER TABLE "BoothPaymentInfos" ALTER COLUMN "Status" SET DEFAULT ('Draft'::character varying);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629131808_AddNightMarketManagementFields') THEN
    CREATE INDEX idx_nightmarket_active_status_created ON "NightMarket" ("IsDeleted", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629131808_AddNightMarketManagementFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260629131808_AddNightMarketManagementFields', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    DROP INDEX "IX_Zones_NightMarketId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    DROP INDEX "IX_MarketLayouts_NightMarketId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    ALTER TABLE "Zones" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    ALTER TABLE "MarketLayouts" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    ALTER TABLE "MarketLayouts" ADD "LayoutName" character varying(150) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    ALTER TABLE "MarketLayouts" ADD "Status" character varying(20) NOT NULL DEFAULT ('Draft'::character varying);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    ALTER TABLE "MarketLayouts" ADD "Version" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    WITH ranked AS (
        SELECT "Id",
               ROW_NUMBER() OVER (
                   PARTITION BY "NightMarketId"
                   ORDER BY "CreatedAt", "Id") AS row_number
        FROM "MarketLayouts"
    )
    UPDATE "MarketLayouts" AS layout
    SET "LayoutName" = 'Layout ' || ranked.row_number,
        "Version" = ranked.row_number
    FROM ranked
    WHERE layout."Id" = ranked."Id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    CREATE UNIQUE INDEX ux_zone_market_name_active ON "Zones" ("NightMarketId", "ZoneName") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    CREATE UNIQUE INDEX ux_marketlayout_market_name_active ON "MarketLayouts" ("NightMarketId", "LayoutName") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    CREATE UNIQUE INDEX ux_marketlayout_market_version_active ON "MarketLayouts" ("NightMarketId", "Version") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    CREATE UNIQUE INDEX ux_marketlayout_one_active_per_market ON "MarketLayouts" ("NightMarketId") WHERE "IsDeleted" = false AND "Status" = 'Active';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629133705_AddZoneAndMarketLayoutManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260629133705_AddZoneAndMarketLayoutManagement', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    DROP INDEX "BoothLocations_BoothId_key";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutNodes" ADD "IsAccessible" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutNodes" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutNodes" ADD "IsStartingPoint" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutNodes" ADD "NodeType" character varying(20) NOT NULL DEFAULT ('Junction'::character varying);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutNodes" ADD "ZoneId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutEdges" ADD "IsAccessible" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutEdges" ADD "IsBidirectional" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutEdges" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutEdges" ADD "LayoutId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "BoothLocations" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "BoothLocations" ADD "LayoutNodeId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "BoothLocations" ADD "ReleasedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "BoothLocations" ADD "SlotNumber" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "BoothLocations" ADD "ZoneId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    UPDATE "LayoutEdges" AS edge
    SET "LayoutId" = node."LayoutId"
    FROM "LayoutNodes" AS node
    WHERE node."Id" = edge."FromNodeId";

    INSERT INTO "LayoutNodes"
        ("Id", "LayoutId", "NodeName", "NodeType", "XCoordinate", "YCoordinate",
         "IsAccessible", "IsStartingPoint", "IsDeleted", "CreatedAt", "UpdatedAt")
    SELECT location."Id", location."LayoutId", 'Legacy booth access', 'BoothAccess',
           location."XCoordinate", location."YCoordinate", true, false, false,
           location."CreatedAt", location."UpdatedAt"
    FROM "BoothLocations" AS location
    WHERE NOT EXISTS (
        SELECT 1 FROM "LayoutNodes" AS node WHERE node."Id" = location."Id");

    UPDATE "BoothLocations"
    SET "LayoutNodeId" = "Id";

    WITH duplicates AS (
        SELECT "Id", ROW_NUMBER() OVER (
            PARTITION BY "LayoutId", "FromNodeId", "ToNodeId"
            ORDER BY "CreatedAt", "Id") AS row_number
        FROM "LayoutEdges"
    )
    UPDATE "LayoutEdges" AS edge
    SET "IsDeleted" = true
    FROM duplicates
    WHERE edge."Id" = duplicates."Id" AND duplicates.row_number > 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutEdges" ALTER COLUMN "LayoutId" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "BoothLocations" ALTER COLUMN "LayoutNodeId" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    CREATE INDEX "IX_LayoutNodes_ZoneId" ON "LayoutNodes" ("ZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    CREATE INDEX "IX_LayoutEdges_FromNodeId" ON "LayoutEdges" ("FromNodeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    CREATE INDEX "IX_LayoutEdges_ToNodeId" ON "LayoutEdges" ("ToNodeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    CREATE UNIQUE INDEX ux_layoutedge_active ON "LayoutEdges" ("LayoutId", "FromNodeId", "ToNodeId") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    CREATE INDEX "IX_BoothLocations_ZoneId" ON "BoothLocations" ("ZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    CREATE UNIQUE INDEX ux_boothlocation_active_booth ON "BoothLocations" ("BoothId") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    CREATE UNIQUE INDEX ux_boothlocation_active_node ON "BoothLocations" ("LayoutNodeId") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "BoothLocations" ADD CONSTRAINT "BoothLocations_LayoutNodeId_fkey" FOREIGN KEY ("LayoutNodeId") REFERENCES "LayoutNodes" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "BoothLocations" ADD CONSTRAINT "BoothLocations_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES "Zones" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutEdges" ADD CONSTRAINT "LayoutEdges_FromNodeId_fkey" FOREIGN KEY ("FromNodeId") REFERENCES "LayoutNodes" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutEdges" ADD CONSTRAINT "LayoutEdges_LayoutId_fkey" FOREIGN KEY ("LayoutId") REFERENCES "MarketLayouts" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutEdges" ADD CONSTRAINT "LayoutEdges_ToNodeId_fkey" FOREIGN KEY ("ToNodeId") REFERENCES "LayoutNodes" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    ALTER TABLE "LayoutNodes" ADD CONSTRAINT "LayoutNodes_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES "Zones" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629143119_CompleteMapAndNavigationFlows') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260629143119_CompleteMapAndNavigationFlows', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629150303_AddSoftDeleteToPackagesAndPrices') THEN
    ALTER TABLE "PackagePrice" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629150303_AddSoftDeleteToPackagesAndPrices') THEN
    ALTER TABLE "Package" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629150303_AddSoftDeleteToPackagesAndPrices') THEN
    ALTER TABLE "FoodPrice" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260629150303_AddSoftDeleteToPackagesAndPrices') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260629150303_AddSoftDeleteToPackagesAndPrices', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701015728_AddCartManagement') THEN
    CREATE TABLE "Cart" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "CustomerId" uuid NOT NULL,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "Cart_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "Cart_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES "User" ("Id")
    );
    COMMENT ON TABLE "Cart" IS 'Giỏ hàng hiện tại của khách hàng';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701015728_AddCartManagement') THEN
    CREATE TABLE "CartItem" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "CartId" uuid NOT NULL,
        "FoodItemId" uuid NOT NULL,
        "Quantity" integer NOT NULL,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "CartItem_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "CartItem_CartId_fkey" FOREIGN KEY ("CartId") REFERENCES "Cart" ("Id"),
        CONSTRAINT "CartItem_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES "FoodItem" ("Id")
    );
    COMMENT ON TABLE "CartItem" IS 'Món ăn trong giỏ hàng';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701015728_AddCartManagement') THEN
    CREATE UNIQUE INDEX ux_cart_active_customer ON "Cart" ("CustomerId") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701015728_AddCartManagement') THEN
    CREATE INDEX idx_cartitem_cart ON "CartItem" ("CartId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701015728_AddCartManagement') THEN
    CREATE INDEX idx_cartitem_fooditem ON "CartItem" ("FoodItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701015728_AddCartManagement') THEN
    CREATE UNIQUE INDEX ux_cartitem_active_food ON "CartItem" ("CartId", "FoodItemId") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701015728_AddCartManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260701015728_AddCartManagement', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER INDEX "IX_Promotion_BoothId" RENAME TO idx_promotion_booth;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "PromotionUsages" ADD "AppliedAt" timestamp with time zone NOT NULL DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "PromotionUsages" ADD "DiscountAmount" numeric(12,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "PromotionUsages" ADD "ReleasedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "PromotionUsages" ADD "Status" character varying(20) NOT NULL DEFAULT ('Reserved'::character varying);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "Promotion" ALTER COLUMN "Status" SET DEFAULT ('Scheduled'::character varying);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    UPDATE "Promotion"
    SET "Status" = 'Scheduled'
    WHERE "Status" = 'Draft';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "Promotion" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "Promotion" ADD "IsPublic" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "Promotion" ADD "MaximumDiscountAmount" numeric(12,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "Promotion" ADD "MinimumOrderAmount" numeric(12,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "Promotion" ADD "Scope" character varying(30) NOT NULL DEFAULT 'EntireBoothOrder';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "Promotion" ADD "TotalUsageLimit" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    ALTER TABLE "Promotion" ADD "UsageLimitPerCustomer" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    CREATE TABLE "PromotionCategory" (
        "PromotionId" uuid NOT NULL,
        "CategoryId" uuid NOT NULL,
        CONSTRAINT "PromotionCategory_pkey" PRIMARY KEY ("PromotionId", "CategoryId"),
        CONSTRAINT "PromotionCategory_CategoryId_fkey" FOREIGN KEY ("CategoryId") REFERENCES "FoodCategories" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "PromotionCategory_PromotionId_fkey" FOREIGN KEY ("PromotionId") REFERENCES "Promotion" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    CREATE TABLE "PromotionFoodItem" (
        "PromotionId" uuid NOT NULL,
        "FoodItemId" uuid NOT NULL,
        CONSTRAINT "PromotionFoodItem_pkey" PRIMARY KEY ("PromotionId", "FoodItemId"),
        CONSTRAINT "PromotionFoodItem_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES "FoodItem" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "PromotionFoodItem_PromotionId_fkey" FOREIGN KEY ("PromotionId") REFERENCES "Promotion" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    CREATE UNIQUE INDEX ux_promotion_active_code ON "Promotion" ("BoothId", "PromotionCode") WHERE "PromotionCode" IS NOT NULL AND "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    CREATE INDEX idx_promotioncategory_category ON "PromotionCategory" ("CategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    CREATE INDEX idx_promotionfooditem_food ON "PromotionFoodItem" ("FoodItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260701052902_AddPromotionManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260701052902_AddPromotionManagement', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    ALTER TABLE "Notification" DROP CONSTRAINT "Notification_BoothId_fkey";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    ALTER TABLE "Notification" ADD "DataJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    ALTER TABLE "Notification" ADD "IsDeleted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    ALTER TABLE "Notification" ADD "IsRead" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    ALTER TABLE "Notification" ADD "ReadAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    ALTER TABLE "Notification" ADD "ReferenceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    ALTER TABLE "Notification" ADD "ReferenceType" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    CREATE TABLE "UserDeviceToken" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "UserId" uuid NOT NULL,
        "Token" character varying(4096) NOT NULL,
        "Platform" character varying(20) NOT NULL,
        "DeviceId" character varying(200),
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "LastUsedAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "UserDeviceToken_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "UserDeviceToken_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "UserDeviceToken" IS 'FCM device tokens registered by users';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    CREATE INDEX idx_device_token_user_active ON "UserDeviceToken" ("UserId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    CREATE UNIQUE INDEX "UserDeviceToken_Token_key" ON "UserDeviceToken" ("Token");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    ALTER TABLE "Notification" ADD CONSTRAINT "Notification_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702125520_AddNotificationManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260702125520_AddNotificationManagement', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702162004_EnforceOneBoothPerOwner') THEN
    DROP INDEX idx_booth_owner;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702162004_EnforceOneBoothPerOwner') THEN
    DROP INDEX "IX_BoothRegistrations_OwnerId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702162004_EnforceOneBoothPerOwner') THEN
    CREATE UNIQUE INDEX uq_booth_owner ON "Booth" ("BoothOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702162004_EnforceOneBoothPerOwner') THEN
    CREATE UNIQUE INDEX uq_pending_booth_registration_owner ON "BoothRegistrations" ("OwnerId") WHERE "Status" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702162004_EnforceOneBoothPerOwner') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260702162004_EnforceOneBoothPerOwner', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706033556_PaymentMethodObj') THEN
    ALTER TABLE "Order" ADD "BoothOwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706033556_PaymentMethodObj') THEN
    CREATE TABLE "PaymentMethods" (
        "Id" uuid NOT NULL,
        "MethodType" text NOT NULL,
        "PaymentToken" text NOT NULL,
        "IsDefault" boolean NOT NULL,
        "UserId" uuid NOT NULL,
        CONSTRAINT "PK_PaymentMethods" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PaymentMethods_User_UserId" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706033556_PaymentMethodObj') THEN
    CREATE INDEX "IX_Order_BoothOwnerId" ON "Order" ("BoothOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706033556_PaymentMethodObj') THEN
    CREATE INDEX "IX_PaymentMethods_UserId" ON "PaymentMethods" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706033556_PaymentMethodObj') THEN
    ALTER TABLE "Order" ADD CONSTRAINT "FK_Order_User_BoothOwnerId" FOREIGN KEY ("BoothOwnerId") REFERENCES "User" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706033556_PaymentMethodObj') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260706033556_PaymentMethodObj', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN

                    UPDATE "Order" SET "OrderCode" = sub.new_code::text
                    FROM (
                        SELECT "Id",
                               (COALESCE(
                                   (SELECT MAX(ord."OrderCode"::bigint)
                                    FROM "Order" ord
                                    WHERE ord."OrderCode" ~ '^\d+$'),
                                   0
                               ) + ROW_NUMBER() OVER (ORDER BY "CreatedAt"))::text AS new_code
                        FROM "Order"
                        WHERE "OrderCode" !~ '^\d+$'
                    ) sub
                    WHERE "Order"."Id" = sub."Id";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Order" ALTER COLUMN "OrderCode" TYPE bigint USING "OrderCode"::bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Order" DROP CONSTRAINT "FK_Order_User_BoothOwnerId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "PaymentMethods" DROP CONSTRAINT "FK_PaymentMethods_User_UserId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "PaymentMethods" DROP CONSTRAINT "PK_PaymentMethods";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Payments" DROP COLUMN "Currency";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Payments" DROP COLUMN "Gateway";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Order" DROP COLUMN "PayStatus";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "PaymentMethods" RENAME TO "PaymentMethod";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER INDEX "IX_PaymentMethods_UserId" RENAME TO "IX_PaymentMethod_UserId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    COMMENT ON TABLE "Order" IS 'Đơn hàng của khách (1 đơn chỉ thuộc về 1 quán)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    COMMENT ON TABLE "PaymentMethod" IS 'Cấu hình phương thức thanh toán ưu tiên của người dùng';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    COMMENT ON COLUMN "Payments"."Type" IS 'Tiền mặt hoặc PayOS';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Payments" ALTER COLUMN "RefundReason" TYPE character varying(500);
    COMMENT ON COLUMN "Payments"."RefundReason" IS 'Lý do hoàn tiền (Nếu có)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    COMMENT ON COLUMN "Payments"."PaidAt" IS 'Thời điểm dòng tiền thực tế được khách hàng quét mã và bắn về hệ thống thành công';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    COMMENT ON COLUMN "Payments"."GatewayRef" IS 'Mã tra soát thực tế của ngân hàng (Ví dụ mã giao dịch của BIDV...)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Payments" ADD "CheckoutUrl" character varying(2000);
    COMMENT ON COLUMN "Payments"."CheckoutUrl" IS 'Đường link thanh toán VietQR động ngắn hạn do PayOS trả về';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Payments" ADD "PaymentLinkId" character varying(255);
    COMMENT ON COLUMN "Payments"."PaymentLinkId" IS 'ID quản lý liên kết link thanh toán của hệ thống PayOS';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Order" ALTER COLUMN "Status" TYPE character varying(30);
    ALTER TABLE "Order" ALTER COLUMN "Status" SET DEFAULT ('Placed'::character varying);
    COMMENT ON COLUMN "Order"."Status" IS 'Placed | Preparing | ReadyForPickup | Completed | Cancelled';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Order" ALTER COLUMN "OrderCode" TYPE bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "PaymentMethod" ALTER COLUMN "PaymentToken" TYPE character varying(500);
    ALTER TABLE "PaymentMethod" ALTER COLUMN "PaymentToken" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "PaymentMethod" ALTER COLUMN "MethodType" TYPE character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "PaymentMethod" ADD CONSTRAINT "PK_PaymentMethod" PRIMARY KEY ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "Order" ADD CONSTRAINT "Order_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES "User" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    ALTER TABLE "PaymentMethod" ADD CONSTRAINT "FK_PaymentMethod_User_UserId" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712101701_EditForPaymentAndOrder') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260712101701_EditForPaymentAndOrder', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE TABLE "AIRecommendationLog" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "CustomerId" uuid,
        "NightMarketId" uuid,
        "RecommendationType" character varying(30) NOT NULL,
        "InputJson" jsonb NOT NULL,
        "ParsedIntentJson" jsonb,
        "ResultJson" jsonb NOT NULL,
        "SelectedOptionId" character varying(100),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "AIRecommendationLog_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "AIRecommendationLog_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES "User" ("Id") ON DELETE SET NULL,
        CONSTRAINT "AIRecommendationLog_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES "NightMarket" ("Id") ON DELETE SET NULL
    );
    COMMENT ON TABLE "AIRecommendationLog" IS 'Log tối giản cho các lần AI recommendation để debug/demo';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE TABLE "FoodTag" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "Name" character varying(100) NOT NULL,
        "Code" character varying(100) NOT NULL,
        "Description" text,
        "TagGroup" character varying(30) NOT NULL,
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "FoodTag_pkey" PRIMARY KEY ("Id")
    );
    COMMENT ON TABLE "FoodTag" IS 'Danh sách tag chuẩn mô tả ngữ nghĩa món ăn cho AI/recommendation';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE TABLE "CustomerPreference" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "CustomerId" uuid NOT NULL,
        "FoodTagId" uuid NOT NULL,
        "PreferenceKind" character varying(20) NOT NULL,
        "PreferenceSource" character varying(30) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "CustomerPreference_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "CustomerPreference_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES "User" ("Id") ON DELETE CASCADE,
        CONSTRAINT "CustomerPreference_FoodTagId_fkey" FOREIGN KEY ("FoodTagId") REFERENCES "FoodTag" ("Id") ON DELETE RESTRICT
    );
    COMMENT ON TABLE "CustomerPreference" IS 'Sở thích rõ ràng của khách hàng theo FoodTag: Like/Avoid';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE TABLE "FoodItemTag" (
        "FoodItemId" uuid NOT NULL,
        "FoodTagId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "FoodItemTag_pkey" PRIMARY KEY ("FoodItemId", "FoodTagId"),
        CONSTRAINT "FoodItemTag_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES "FoodItem" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FoodItemTag_FoodTagId_fkey" FOREIGN KEY ("FoodTagId") REFERENCES "FoodTag" ("Id") ON DELETE RESTRICT
    );
    COMMENT ON TABLE "FoodItemTag" IS 'Bảng nối gắn tag ngữ nghĩa vào món ăn';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE INDEX idx_airecommendationlog_customer ON "AIRecommendationLog" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE INDEX idx_airecommendationlog_nightmarket ON "AIRecommendationLog" ("NightMarketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE INDEX idx_airecommendationlog_type_created ON "AIRecommendationLog" ("RecommendationType", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE INDEX idx_customerpreference_customer ON "CustomerPreference" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE INDEX idx_customerpreference_foodtag ON "CustomerPreference" ("FoodTagId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE UNIQUE INDEX ux_customerpreference_tag_kind ON "CustomerPreference" ("CustomerId", "FoodTagId", "PreferenceKind");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE INDEX idx_fooditemtag_foodtag ON "FoodItemTag" ("FoodTagId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE UNIQUE INDEX ux_foodtag_code_active ON "FoodTag" ("Code") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    CREATE UNIQUE INDEX ux_foodtag_name_active ON "FoodTag" ("Name") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260712164611_AddAIModule') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260712164611_AddAIModule', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713083049_AddPolicyViolationToComplaint') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713083049_AddPolicyViolationToComplaint', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713101102_SeedVietnamNightMarketAIData') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713101102_SeedVietnamNightMarketAIData', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713123832_AddUserStatusHistoryAndEmailOutbox') THEN
    CREATE TABLE "EmailOutbox" (
        "Id" uuid NOT NULL,
        "RecipientEmail" text NOT NULL,
        "Subject" text NOT NULL,
        "HtmlBody" text NOT NULL,
        "EmailType" text NOT NULL,
        "ReferenceId" uuid NOT NULL,
        "Status" text NOT NULL,
        "RetryCount" integer NOT NULL,
        "LastError" text,
        "NextRetryAt" timestamp with time zone,
        "SentAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_EmailOutbox" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713123832_AddUserStatusHistoryAndEmailOutbox') THEN
    CREATE TABLE "UserStatusHistories" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ChangedByAdminId" uuid NOT NULL,
        "PreviousStatus" integer NOT NULL,
        "NewStatus" integer NOT NULL,
        "Reason" character varying(1000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserStatusHistories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserStatusHistories_User_ChangedByAdminId" FOREIGN KEY ("ChangedByAdminId") REFERENCES "User" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_UserStatusHistories_User_UserId" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713123832_AddUserStatusHistoryAndEmailOutbox') THEN
    CREATE UNIQUE INDEX "IX_EmailOutbox_ReferenceId_EmailType" ON "EmailOutbox" ("ReferenceId", "EmailType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713123832_AddUserStatusHistoryAndEmailOutbox') THEN
    CREATE INDEX "IX_UserStatusHistories_ChangedByAdminId" ON "UserStatusHistories" ("ChangedByAdminId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713123832_AddUserStatusHistoryAndEmailOutbox') THEN
    CREATE INDEX "IX_UserStatusHistories_UserId_CreatedAt" ON "UserStatusHistories" ("UserId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713123832_AddUserStatusHistoryAndEmailOutbox') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713123832_AddUserStatusHistoryAndEmailOutbox', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713125625_AlterUserStatusHistoryStatusToString') THEN

    ALTER TABLE "UserStatusHistories"
    ALTER COLUMN "PreviousStatus" TYPE varchar(20)
    USING CASE "PreviousStatus"
        WHEN 1 THEN 'Active'
        WHEN 4 THEN 'Inactive'
        ELSE NULL
    END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713125625_AlterUserStatusHistoryStatusToString') THEN

    ALTER TABLE "UserStatusHistories"
    ALTER COLUMN "NewStatus" TYPE varchar(20)
    USING CASE "NewStatus"
        WHEN 1 THEN 'Active'
        WHEN 4 THEN 'Inactive'
        ELSE NULL
    END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713125625_AlterUserStatusHistoryStatusToString') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713125625_AlterUserStatusHistoryStatusToString', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713173605_AdminSchemaUpdates') THEN
    UPDATE "User" SET "Status" = 'Inactive' WHERE "Status" IN ('Suspended', 'Banned', '2', '3', '4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713173605_AdminSchemaUpdates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713173605_AdminSchemaUpdates', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714012052_AddMarketSubscriptions') THEN
    ALTER TABLE "Package" ADD "Type" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714012052_AddMarketSubscriptions') THEN
    ALTER TABLE "BoothSubscriptions" ADD "AdminNotes" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714012052_AddMarketSubscriptions') THEN
    ALTER TABLE "BoothSubscriptions" ADD "PaymentEvidenceUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714012052_AddMarketSubscriptions') THEN
    CREATE TABLE "MarketSubscriptions" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "MarketOwnerId" uuid NOT NULL,
        "PackageId" uuid NOT NULL,
        "StartDate" timestamp with time zone NOT NULL,
        "EndDate" timestamp with time zone NOT NULL,
        "Status" character varying(20) NOT NULL DEFAULT ('Active'::character varying),
        "PaymentEvidenceUrl" text,
        "AdminNotes" text,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "MarketSubscriptions_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "MarketSubscriptions_MarketOwnerId_fkey" FOREIGN KEY ("MarketOwnerId") REFERENCES "User" ("Id") ON DELETE CASCADE,
        CONSTRAINT "MarketSubscriptions_PackageId_fkey" FOREIGN KEY ("PackageId") REFERENCES "Package" ("Id")
    );
    COMMENT ON TABLE "MarketSubscriptions" IS 'Lịch sử đăng ký gói dịch vụ của Market Owner';
    COMMENT ON COLUMN "MarketSubscriptions"."Status" IS 'Active | Expired | Cancelled | PendingPayment';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714012052_AddMarketSubscriptions') THEN
    CREATE INDEX "IX_MarketSubscriptions_MarketOwnerId" ON "MarketSubscriptions" ("MarketOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714012052_AddMarketSubscriptions') THEN
    CREATE INDEX "IX_MarketSubscriptions_PackageId" ON "MarketSubscriptions" ("PackageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714012052_AddMarketSubscriptions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714012052_AddMarketSubscriptions', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714031959_AddSystemSettingAndPaidAmount') THEN
    ALTER TABLE "NightMarket" ADD "MarketOwnerId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714031959_AddSystemSettingAndPaidAmount') THEN
    ALTER TABLE "MarketSubscriptions" ADD "PaidAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714031959_AddSystemSettingAndPaidAmount') THEN
    ALTER TABLE "BoothSubscriptions" ADD "PaidAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714031959_AddSystemSettingAndPaidAmount') THEN
    CREATE TABLE "SystemSetting" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "Key" character varying(100) NOT NULL,
        "Value" text NOT NULL,
        "Description" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_SystemSetting" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714031959_AddSystemSettingAndPaidAmount') THEN
    CREATE INDEX "IX_NightMarket_MarketOwnerId" ON "NightMarket" ("MarketOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714031959_AddSystemSettingAndPaidAmount') THEN
    CREATE UNIQUE INDEX "IX_SystemSetting_Key" ON "SystemSetting" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714031959_AddSystemSettingAndPaidAmount') THEN
    ALTER TABLE "NightMarket" ADD CONSTRAINT "NightMarket_MarketOwnerId_fkey" FOREIGN KEY ("MarketOwnerId") REFERENCES "User" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714031959_AddSystemSettingAndPaidAmount') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714031959_AddSystemSettingAndPaidAmount', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714052831_FixZonesDefaults') THEN
    ALTER TABLE "Zones" ALTER COLUMN "Status" SET DEFAULT ('Active'::character varying);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714052831_FixZonesDefaults') THEN
    ALTER TABLE "Zones" ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714052831_FixZonesDefaults') THEN
    ALTER TABLE "Zones" ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714052831_FixZonesDefaults') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714052831_FixZonesDefaults', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714075433_NormalizeComplaintStatus') THEN

    UPDATE "Complaints"
    SET "Status" = CASE
        WHEN "Status" IN ('Submitted', 'Open', 'UnderInvestigation', 'InProgress') THEN 'Pending'
        WHEN "Status" IN ('Closed') THEN 'Resolved'
        WHEN "Status" IN ('Resolved') THEN 'Resolved'
        WHEN "Status" IN ('Rejected') THEN 'Rejected'
        WHEN "Status" IN ('Pending') THEN 'Pending'
        ELSE 'Pending'
    END;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714075433_NormalizeComplaintStatus') THEN

    ALTER TABLE "Complaints" ALTER COLUMN "Status" SET DEFAULT 'Pending'::character varying;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714075433_NormalizeComplaintStatus') THEN

    COMMENT ON COLUMN "Complaints"."Status" IS 'Pending | Resolved | Rejected';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714075433_NormalizeComplaintStatus') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714075433_NormalizeComplaintStatus', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714081048_SyncComplaintStatusMetadata') THEN
    ALTER TABLE "Complaints" ALTER COLUMN "Status" SET DEFAULT ('Pending'::character varying);
    COMMENT ON COLUMN "Complaints"."Status" IS 'Pending | Resolved | Rejected';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714081048_SyncComplaintStatusMetadata') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714081048_SyncComplaintStatusMetadata', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714090000_RepairUserStatusHistoryStatusColumns') THEN

    ALTER TABLE "UserStatusHistories"
    ALTER COLUMN "PreviousStatus" TYPE varchar(20)
    USING CASE
        WHEN "PreviousStatus"::text IN ('1', 'Active') THEN 'Active'
        WHEN "PreviousStatus"::text IN ('2', '4', 'Inactive') THEN 'Inactive'
        ELSE 'Inactive'
    END;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714090000_RepairUserStatusHistoryStatusColumns') THEN

    ALTER TABLE "UserStatusHistories"
    ALTER COLUMN "NewStatus" TYPE varchar(20)
    USING CASE
        WHEN "NewStatus"::text IN ('1', 'Active') THEN 'Active'
        WHEN "NewStatus"::text IN ('2', '4', 'Inactive') THEN 'Inactive'
        ELSE 'Inactive'
    END;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714090000_RepairUserStatusHistoryStatusColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714090000_RepairUserStatusHistoryStatusColumns', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714150000_NormalizeUserStatusValues') THEN

    UPDATE "User"
    SET "Status" = CASE
        WHEN "Status"::text IN ('Banned', 'Suspended') THEN 'Inactive'
        WHEN "Status"::text IN ('Active', 'Inactive') THEN "Status"
        ELSE 'Inactive'
    END
    WHERE "Status"::text NOT IN ('Active', 'Inactive');

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714150000_NormalizeUserStatusValues') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714150000_NormalizeUserStatusValues', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160000_NormalizeComplaintResolutionAction') THEN

    UPDATE "Complaints"
    SET "ResolutionAction" = 0
    WHERE "ResolutionAction" IS NULL
      AND "Status"::text IN ('1', 'Resolved');

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160000_NormalizeComplaintResolutionAction') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714160000_NormalizeComplaintResolutionAction', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714233644_ClearRejectedResolutionAction') THEN

    UPDATE "Complaints"
    SET "ResolutionAction" = NULL, "PolicyViolation" = NULL
    WHERE "Status" = 'Rejected';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714233644_ClearRejectedResolutionAction') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714233644_ClearRejectedResolutionAction', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN
    ALTER TABLE "PackagePrice" ADD "DurationDays" integer NOT NULL DEFAULT 30;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN
    ALTER TABLE "Package" ADD "Code" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN
    ALTER TABLE "Package" ADD "Entitlements" jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN
    CREATE UNIQUE INDEX "IX_Package_Code" ON "Package" ("Code") WHERE "Code" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoothSubscriptions_BoothId_Active"
    ON "BoothSubscriptions" ("BoothId")
    WHERE "Status" = 'Active';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoothSubscriptions_BoothId_PendingPayment"
    ON "BoothSubscriptions" ("BoothId")
    WHERE "Status" = 'PendingPayment';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketSubscriptions_MarketOwnerId_Active"
    ON "MarketSubscriptions" ("MarketOwnerId")
    WHERE "Status" = 'Active';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN

    CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketSubscriptions_MarketOwnerId_PendingPayment"
    ON "MarketSubscriptions" ("MarketOwnerId")
    WHERE "Status" = 'PendingPayment';

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715021554_AddPackageCodeEntitlements') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260715021554_AddPackageCodeEntitlements', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    DROP INDEX "IX_Message_SenderId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    ALTER TABLE "Message" ADD "ClientMessageId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    ALTER TABLE "Message" ADD "DeletedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    ALTER TABLE "Message" ADD "ReadAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    ALTER TABLE "Conversations" ADD "BoothOwnerLastReadAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    ALTER TABLE "Conversations" ADD "CustomerLastReadAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    ALTER TABLE "Conversations" ADD "LastMessageAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    ALTER TABLE "Conversations" ADD "LastMessageId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    CREATE UNIQUE INDEX ux_message_sender_client_message ON "Message" ("SenderId", "ClientMessageId") WHERE "ClientMessageId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    CREATE INDEX idx_conversation_last_message ON "Conversations" ("LastMessageAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    CREATE INDEX "IX_Conversations_LastMessageId" ON "Conversations" ("LastMessageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    ALTER TABLE "Conversations" ADD CONSTRAINT "Conversations_LastMessageId_fkey" FOREIGN KEY ("LastMessageId") REFERENCES "Message" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063023_AddChatRealtimeFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260717063023_AddChatRealtimeFields', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063805_ConvertEntityFieldsToEnums') THEN
    UPDATE "User"
    SET "AuthProvider" = 'LocalGoogle'
    WHERE "AuthProvider" = 'Local,Google';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717063805_ConvertEntityFieldsToEnums') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260717063805_ConvertEntityFieldsToEnums', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717113421_AddAdminUserStatusManagement') THEN
    UPDATE "User"
    SET "Status" = CASE
        WHEN "Status"::text IN ('Banned', 'Suspended') THEN 'Inactive'
        WHEN "Status"::text IN ('Active', 'Inactive') THEN "Status"
        ELSE 'Inactive'
    END
    WHERE "Status"::text NOT IN ('Active', 'Inactive');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717113421_AddAdminUserStatusManagement') THEN
    CREATE TABLE IF NOT EXISTS "EmailOutbox" (
        "Id" uuid NOT NULL,
        "RecipientEmail" text NOT NULL,
        "Subject" text NOT NULL,
        "HtmlBody" text NOT NULL,
        "EmailType" text NOT NULL,
        "ReferenceId" uuid NOT NULL,
        "Status" text NOT NULL,
        "RetryCount" integer NOT NULL,
        "LastError" text,
        "NextRetryAt" timestamp with time zone,
        "SentAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_EmailOutbox" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717113421_AddAdminUserStatusManagement') THEN
    CREATE TABLE IF NOT EXISTS "UserStatusHistories" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ChangedByAdminId" uuid NOT NULL,
        "PreviousStatus" character varying(20) NOT NULL,
        "NewStatus" character varying(20) NOT NULL,
        "Reason" character varying(1000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserStatusHistories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserStatusHistories_User_ChangedByAdminId"
            FOREIGN KEY ("ChangedByAdminId") REFERENCES "User" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_UserStatusHistories_User_UserId"
            FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717113421_AddAdminUserStatusManagement') THEN
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmailOutbox_ReferenceId_EmailType"
        ON "EmailOutbox" ("ReferenceId", "EmailType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717113421_AddAdminUserStatusManagement') THEN
    CREATE INDEX IF NOT EXISTS "IX_UserStatusHistories_ChangedByAdminId"
        ON "UserStatusHistories" ("ChangedByAdminId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717113421_AddAdminUserStatusManagement') THEN
    CREATE INDEX IF NOT EXISTS "IX_UserStatusHistories_UserId_CreatedAt"
        ON "UserStatusHistories" ("UserId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260717113421_AddAdminUserStatusManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260717113421_AddAdminUserStatusManagement', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718021025_AddIsDeletedToNotification') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718021025_AddIsDeletedToNotification', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718024159_AddAdminNotificationBatchMetadata') THEN

                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "BatchId" uuid;
                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid;
                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "Target" integer;
                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "TargetRole" character varying(50);
                    
                    CREATE INDEX IF NOT EXISTS idx_notification_batch_id ON "Notification" ("BatchId");
                    CREATE INDEX IF NOT EXISTS idx_notification_created_by_created_at ON "Notification" ("CreatedByUserId", "CreatedAt" DESC);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718024159_AddAdminNotificationBatchMetadata') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718024159_AddAdminNotificationBatchMetadata', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718032433_UpdateNotificationTargetRoleMaxLength') THEN
    ALTER TABLE "Notification" ALTER COLUMN "TargetRole" TYPE character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718032433_UpdateNotificationTargetRoleMaxLength') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718032433_UpdateNotificationTargetRoleMaxLength', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718063334_RepairOrderPayStatusColumn') THEN

                    ALTER TABLE "Order"
                    ADD COLUMN IF NOT EXISTS "PayStatus"
                    integer NOT NULL DEFAULT 0;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718063334_RepairOrderPayStatusColumn') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718063334_RepairOrderPayStatusColumn', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718083238_RepairNotificationAndPackageDefaults') THEN

                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "IsRead" boolean NOT NULL DEFAULT false;
                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "ReadAt" timestamp with time zone NULL;
                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "ReferenceType" character varying(100) NULL;
                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "ReferenceId" uuid NULL;
                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "DataJson" text NULL;
                    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT false;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718083238_RepairNotificationAndPackageDefaults') THEN

                    CREATE TABLE IF NOT EXISTS "UserDeviceToken" (
                        "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
                        "UserId" uuid NOT NULL,
                        "Token" character varying(4096) NOT NULL,
                        "Platform" character varying(20) NOT NULL,
                        "DeviceId" character varying(200) NULL,
                        "IsActive" boolean NOT NULL DEFAULT true,
                        "IsDeleted" boolean NOT NULL DEFAULT false,
                        "LastUsedAt" timestamp with time zone NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                        CONSTRAINT "UserDeviceToken_pkey" PRIMARY KEY ("Id"),
                        CONSTRAINT "UserDeviceToken_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "User"("Id") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "UserDeviceToken_Token_key" ON "UserDeviceToken" ("Token");
                    CREATE INDEX IF NOT EXISTS "idx_device_token_user_active" ON "UserDeviceToken" ("UserId", "IsActive");
                    COMMENT ON TABLE "UserDeviceToken" IS 'FCM device tokens registered by users';
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718083238_RepairNotificationAndPackageDefaults') THEN

                    ALTER TABLE "Package" ALTER COLUMN "Status" SET DEFAULT 'Active';
                    ALTER TABLE "Package" ALTER COLUMN "CreatedAt" SET DEFAULT now();
                    ALTER TABLE "Package" ALTER COLUMN "UpdatedAt" SET DEFAULT now();
                    ALTER TABLE "Package" ALTER COLUMN "IsDeleted" SET DEFAULT false;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718083238_RepairNotificationAndPackageDefaults') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718083238_RepairNotificationAndPackageDefaults', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718085453_RepairPackagePriceDefaults') THEN

                    ALTER TABLE "Package" ALTER COLUMN "Status" SET DEFAULT 'Active';
                    ALTER TABLE "Package" ALTER COLUMN "CreatedAt" SET DEFAULT now();
                    ALTER TABLE "Package" ALTER COLUMN "UpdatedAt" SET DEFAULT now();
                    ALTER TABLE "Package" ALTER COLUMN "IsDeleted" SET DEFAULT false;

                    ALTER TABLE "PackagePrice" ALTER COLUMN "CreatedAt" SET DEFAULT now();
                    ALTER TABLE "PackagePrice" ALTER COLUMN "UpdatedAt" SET DEFAULT now();
                    ALTER TABLE "PackagePrice" ALTER COLUMN "IsDeleted" SET DEFAULT false;
                    ALTER TABLE "PackagePrice" ALTER COLUMN "DurationDays" SET DEFAULT 30;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718085453_RepairPackagePriceDefaults') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718085453_RepairPackagePriceDefaults', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718194409_AddPayOSColumns') THEN
    ALTER TABLE "MarketSubscriptions"
        ADD COLUMN IF NOT EXISTS "PaidAt" timestamp with time zone;
    ALTER TABLE "MarketSubscriptions"
        ADD COLUMN IF NOT EXISTS "PayOSOrderCode" bigint;
    ALTER TABLE "MarketSubscriptions"
        ADD COLUMN IF NOT EXISTS "PayOSPaymentLinkId" character varying(100);
    ALTER TABLE "MarketSubscriptions"
        ADD COLUMN IF NOT EXISTS "PaymentExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718194409_AddPayOSColumns') THEN
    ALTER TABLE "BoothSubscriptions"
        ADD COLUMN IF NOT EXISTS "PaidAt" timestamp with time zone;
    ALTER TABLE "BoothSubscriptions"
        ADD COLUMN IF NOT EXISTS "PayOSOrderCode" bigint;
    ALTER TABLE "BoothSubscriptions"
        ADD COLUMN IF NOT EXISTS "PayOSPaymentLinkId" character varying(100);
    ALTER TABLE "BoothSubscriptions"
        ADD COLUMN IF NOT EXISTS "PaymentExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718194409_AddPayOSColumns') THEN
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketSubscriptions_PayOSOrderCode"
        ON "MarketSubscriptions" ("PayOSOrderCode") WHERE "PayOSOrderCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718194409_AddPayOSColumns') THEN
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoothSubscriptions_PayOSOrderCode"
        ON "BoothSubscriptions" ("PayOSOrderCode") WHERE "PayOSOrderCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718194409_AddPayOSColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718194409_AddPayOSColumns', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718215130_AddAdminNotificationBatchMetadata') THEN
    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "BatchId" uuid;
    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid;
    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "Target" integer;
    ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "TargetRole" character varying(50);

    CREATE INDEX IF NOT EXISTS idx_notification_batch_id
        ON "Notification" ("BatchId");
    CREATE INDEX IF NOT EXISTS idx_notification_created_by_created_at
        ON "Notification" ("CreatedByUserId", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260718215130_AddAdminNotificationBatchMetadata') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260718215130_AddAdminNotificationBatchMetadata', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719060000_AddModerationStatusAndHistory') THEN
    ALTER TABLE "NightMarket" ADD "ModerationStatus" character varying(20) NOT NULL DEFAULT 'Active';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719060000_AddModerationStatusAndHistory') THEN

                    ALTER TABLE "NightMarket"
                    ALTER COLUMN "ModerationStatus" SET DEFAULT 'Active'::character varying;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719060000_AddModerationStatusAndHistory') THEN
    CREATE INDEX idx_nightmarket_moderation_status ON "NightMarket" ("ModerationStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719060000_AddModerationStatusAndHistory') THEN
    CREATE TABLE "ModerationActionHistory" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "BoothId" uuid,
        "NightMarketId" uuid,
        "AdminId" uuid NOT NULL,
        "AdminName" character varying(200),
        "PreviousStatus" character varying(20) NOT NULL,
        "NewStatus" character varying(20) NOT NULL,
        "Reason" character varying(1000) NOT NULL,
        "Source" character varying(20) NOT NULL DEFAULT ('DirectAdmin'::character varying),
        "ComplaintId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "ModerationActionHistory_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "ModerationActionHistory_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE SET NULL,
        CONSTRAINT "ModerationActionHistory_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES "NightMarket" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719060000_AddModerationStatusAndHistory') THEN
    CREATE INDEX idx_moderationhistory_booth ON "ModerationActionHistory" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719060000_AddModerationStatusAndHistory') THEN
    CREATE INDEX idx_moderationhistory_nightmarket ON "ModerationActionHistory" ("NightMarketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719060000_AddModerationStatusAndHistory') THEN
    CREATE INDEX idx_moderationhistory_created ON "ModerationActionHistory" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719060000_AddModerationStatusAndHistory') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260719060000_AddModerationStatusAndHistory', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720000000_AddPayOSOrderCodeSequence') THEN

                    CREATE SEQUENCE IF NOT EXISTS payos_order_code_seq
                        INCREMENT 1
                        START 1
                        MINVALUE 1
                        MAXVALUE 99999999999999
                        NO CYCLE;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720000000_AddPayOSOrderCodeSequence') THEN

                    SELECT setval(
                        'payos_order_code_seq',
                        GREATEST(
                            1,
                            COALESCE(
                                (SELECT MAX("OrderCode" % 100000000000000)
                                 FROM "Order"
                                 WHERE "OrderCode" >= 100000000000000
                                   AND "OrderCode" <  200000000000000),
                                0
                            ),
                            COALESCE(
                                (SELECT MAX("PayOSOrderCode" % 100000000000000)
                                 FROM "BoothSubscriptions"
                                 WHERE "PayOSOrderCode" IS NOT NULL
                                   AND "PayOSOrderCode" >= 200000000000000
                                   AND "PayOSOrderCode" <  300000000000000),
                                0
                            ),
                            COALESCE(
                                (SELECT MAX("PayOSOrderCode" % 100000000000000)
                                 FROM "MarketSubscriptions"
                                 WHERE "PayOSOrderCode" IS NOT NULL
                                   AND "PayOSOrderCode" >= 300000000000000
                                   AND "PayOSOrderCode" <  400000000000000),
                                0
                            )
                        )
                    );
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720000000_AddPayOSOrderCodeSequence') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260720000000_AddPayOSOrderCodeSequence', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720120000_AddPayOSOrderCodeToPayment') THEN
    ALTER TABLE "Payments"
        ADD COLUMN IF NOT EXISTS "Gateway" character varying(20);

    UPDATE "Payments"
    SET "Gateway" = CASE
        WHEN "Type" = 'PayOS' THEN 'Payos'
        ELSE 'BankTransfer'
    END
    WHERE "Gateway" IS NULL OR "Gateway" = '';

    ALTER TABLE "Payments"
        ALTER COLUMN "Gateway" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720120000_AddPayOSOrderCodeToPayment') THEN
    ALTER TABLE "Payments" ADD "PayOSOrderCode" bigint;
    COMMENT ON COLUMN "Payments"."PayOSOrderCode" IS 'PayOS order code for this specific payment transaction (supplemental payments)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720120000_AddPayOSOrderCodeToPayment') THEN
    CREATE UNIQUE INDEX idx_payments_payos_ordercode ON "Payments" ("PayOSOrderCode") WHERE "PayOSOrderCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720120000_AddPayOSOrderCodeToPayment') THEN
    WITH ranked AS (
        SELECT "Id",
               ROW_NUMBER() OVER (
                   PARTITION BY "OrderId"
                   ORDER BY
                       CASE WHEN NULLIF("CheckoutUrl", '') IS NOT NULL THEN 0 ELSE 1 END,
                       "CreatedAt" DESC,
                       "Id"
               ) AS row_number
        FROM "Payments"
        WHERE "Status" = 'Pending' AND "Gateway" = 'Payos'
    )
    UPDATE "Payments" AS payment
    SET "Status" = 'Cancelled', "UpdatedAt" = NOW()
    FROM ranked
    WHERE payment."Id" = ranked."Id" AND ranked.row_number > 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720120000_AddPayOSOrderCodeToPayment') THEN
    CREATE UNIQUE INDEX ux_payments_one_pending_payos_per_order ON "Payments" ("OrderId") WHERE "Status" = 'Pending' AND "Gateway" = 'Payos';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720120000_AddPayOSOrderCodeToPayment') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260720120000_AddPayOSOrderCodeToPayment', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "NightMarket" ADD "DeletedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "NightMarket" ADD "DeletedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "NightMarket" ADD "DeletionReason" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "MarketSubscriptions" ADD "ChangeType" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "MarketSubscriptions" ADD "CreditAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "MarketSubscriptions" ADD "PolicyAcceptedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "MarketSubscriptions" ADD "PolicySnapshotJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "MarketSubscriptions" ADD "PolicyVersion" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "MarketSubscriptions" ADD "PreviousSubscriptionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "BoothSubscriptions" ADD "ChangeType" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "BoothSubscriptions" ADD "CreditAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "BoothSubscriptions" ADD "PausedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "BoothSubscriptions" ADD "PausedRemainingDays" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "BoothSubscriptions" ADD "PolicyAcceptedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "BoothSubscriptions" ADD "PolicySnapshotJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "BoothSubscriptions" ADD "PolicyVersion" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    ALTER TABLE "BoothSubscriptions" ADD "PreviousSubscriptionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    CREATE TABLE "PackagePolicies" (
        "Id" uuid NOT NULL,
        "PackageId" uuid NOT NULL,
        "Version" text NOT NULL,
        "Title" text NOT NULL,
        "ContentJson" text NOT NULL,
        "ContentMarkdown" text,
        "EffectiveFrom" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "IsDeleted" boolean NOT NULL,
        CONSTRAINT "PK_PackagePolicies" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PackagePolicies_Package_PackageId" FOREIGN KEY ("PackageId") REFERENCES "Package" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    CREATE UNIQUE INDEX "IX_PackagePolicies_PackageId_Version" ON "PackagePolicies" ("PackageId", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720140849_PackagePolicies') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260720140849_PackagePolicies', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720145621_AddPackagePolicyPartialUniqueIndex') THEN
    ALTER TABLE "PackagePolicies" ALTER COLUMN "Version" TYPE character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720145621_AddPackagePolicyPartialUniqueIndex') THEN
    ALTER TABLE "PackagePolicies" ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720145621_AddPackagePolicyPartialUniqueIndex') THEN
    ALTER TABLE "PackagePolicies" ALTER COLUMN "Title" TYPE character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720145621_AddPackagePolicyPartialUniqueIndex') THEN
    ALTER TABLE "PackagePolicies" ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720145621_AddPackagePolicyPartialUniqueIndex') THEN

                    WITH ranked AS (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "PackageId"
                                   ORDER BY "EffectiveFrom" DESC, "UpdatedAt" DESC, "CreatedAt" DESC, "Id"
                               ) AS row_number
                        FROM "PackagePolicies"
                        WHERE "IsActive" = TRUE AND "IsDeleted" = FALSE
                    )
                    UPDATE "PackagePolicies" AS policy
                    SET "IsActive" = FALSE,
                        "UpdatedAt" = now()
                    FROM ranked
                    WHERE policy."Id" = ranked."Id"
                      AND ranked.row_number > 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720145621_AddPackagePolicyPartialUniqueIndex') THEN
    CREATE UNIQUE INDEX "IX_PackagePolicies_PackageId_IsActive" ON "PackagePolicies" ("PackageId", "IsActive") WHERE "IsActive" = true AND "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720145621_AddPackagePolicyPartialUniqueIndex') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260720145621_AddPackagePolicyPartialUniqueIndex', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720194905_AddBuyerDetailsToSubscriptions') THEN
    ALTER TABLE "MarketSubscriptions"
        ADD COLUMN IF NOT EXISTS "BuyerName" character varying(200),
        ADD COLUMN IF NOT EXISTS "BuyerEmail" character varying(200),
        ADD COLUMN IF NOT EXISTS "BuyerPhone" character varying(20);

    ALTER TABLE "BoothSubscriptions"
        ADD COLUMN IF NOT EXISTS "BuyerName" character varying(200),
        ADD COLUMN IF NOT EXISTS "BuyerEmail" character varying(200),
        ADD COLUMN IF NOT EXISTS "BuyerPhone" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720194905_AddBuyerDetailsToSubscriptions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260720194905_AddBuyerDetailsToSubscriptions', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723041716_MakeBoothRegistrationIdNullable') THEN
    ALTER TABLE "Booth" DROP CONSTRAINT "FK_Booth_BoothRegistrations_RegistrationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723041716_MakeBoothRegistrationIdNullable') THEN
    DROP INDEX "IX_Booth_RegistrationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723041716_MakeBoothRegistrationIdNullable') THEN
    ALTER TABLE "Booth" ALTER COLUMN "RegistrationId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723041716_MakeBoothRegistrationIdNullable') THEN
    CREATE UNIQUE INDEX "IX_Booth_RegistrationId" ON "Booth" ("RegistrationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723041716_MakeBoothRegistrationIdNullable') THEN
    ALTER TABLE "Booth" ADD CONSTRAINT "Booth_RegistrationId_fkey" FOREIGN KEY ("RegistrationId") REFERENCES "BoothRegistrations" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723041716_MakeBoothRegistrationIdNullable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260723041716_MakeBoothRegistrationIdNullable', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "Booth" DROP CONSTRAINT "Booth_RegistrationId_fkey";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothDocuments" DROP CONSTRAINT "BoothDocuments_BoothId_fkey";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    DROP INDEX "IX_Booth_RegistrationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "ModerationActionHistory" ALTER COLUMN "Id" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "MarketSubscriptions" ALTER COLUMN "PolicyVersion" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "MarketSubscriptions" ALTER COLUMN "CreditAmount" TYPE numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "MarketSubscriptions" ALTER COLUMN "ChangeType" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "MarketSubscriptions" ALTER COLUMN "BuyerPhone" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "MarketSubscriptions" ALTER COLUMN "BuyerName" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "MarketSubscriptions" ALTER COLUMN "BuyerEmail" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "LayoutNodes" ADD "ColumnIndex" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "LayoutNodes" ADD "LayoutBlockId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "LayoutNodes" ADD "RowIndex" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "LayoutNodes" ADD "SlotCode" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothSubscriptions" ALTER COLUMN "PolicyVersion" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothSubscriptions" ALTER COLUMN "CreditAmount" TYPE numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothSubscriptions" ALTER COLUMN "ChangeType" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothSubscriptions" ALTER COLUMN "BuyerPhone" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothSubscriptions" ALTER COLUMN "BuyerName" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothSubscriptions" ALTER COLUMN "BuyerEmail" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothRegistrations" ADD "BoothId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    CREATE TABLE "LayoutBlocks" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "LayoutId" uuid NOT NULL,
        "Type" character varying(50) NOT NULL,
        "Name" character varying(150) NOT NULL,
        "X" double precision NOT NULL,
        "Y" double precision NOT NULL,
        "Width" double precision NOT NULL,
        "Height" double precision NOT NULL,
        "Rotation" double precision NOT NULL,
        "ZoneId" uuid,
        "ConfigJson" text,
        "DisplayOrder" integer NOT NULL,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "LayoutBlocks_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "LayoutBlocks_LayoutId_fkey" FOREIGN KEY ("LayoutId") REFERENCES "MarketLayouts" ("Id") ON DELETE CASCADE,
        CONSTRAINT "LayoutBlocks_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES "Zones" ("Id") ON DELETE SET NULL
    );
    COMMENT ON TABLE "LayoutBlocks" IS 'Nhóm các block layout (dãy gian hàng, lối đi, khu vực, v.v.)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    CREATE INDEX "IX_LayoutNodes_LayoutBlockId" ON "LayoutNodes" ("LayoutBlockId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    CREATE INDEX "IX_BoothRegistrations_BoothId" ON "BoothRegistrations" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    CREATE INDEX "IX_Booth_RegistrationId" ON "Booth" ("RegistrationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    CREATE INDEX "IX_LayoutBlocks_LayoutId" ON "LayoutBlocks" ("LayoutId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    CREATE INDEX "IX_LayoutBlocks_ZoneId" ON "LayoutBlocks" ("ZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "Booth" ADD CONSTRAINT "Booth_RegistrationId_fkey" FOREIGN KEY ("RegistrationId") REFERENCES "BoothRegistrations" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothDocuments" ADD CONSTRAINT "BoothDocuments_RegistrationId_fkey" FOREIGN KEY ("RegistrationId") REFERENCES "BoothRegistrations" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "BoothRegistrations" ADD CONSTRAINT "FK_BoothRegistrations_Booth_BoothId" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    ALTER TABLE "LayoutNodes" ADD CONSTRAINT "LayoutNodes_LayoutBlockId_fkey" FOREIGN KEY ("LayoutBlockId") REFERENCES "LayoutBlocks" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724044848_Phase2_CinemaLayoutModels') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260724044848_Phase2_CinemaLayoutModels', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724073716_RepairPhase2CinemaLayout') THEN
    CREATE UNIQUE INDEX ux_layoutnodes_active_layout_slotcode ON "LayoutNodes" ("LayoutId", "SlotCode") WHERE "IsDeleted" = false AND "SlotCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724073716_RepairPhase2CinemaLayout') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260724073716_RepairPhase2CinemaLayout', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724122144_NormalizeNightMarketOperationalStatus') THEN
    UPDATE "NightMarket"
    SET "ModerationStatus" = 'Suspended',
        "Status" = 'Inactive'
    WHERE "Status" = 'Suspended';

    UPDATE "NightMarket"
    SET "Status" = 'Active'
    WHERE "Status" = 'Open';

    UPDATE "NightMarket"
    SET "Status" = 'Inactive'
    WHERE "Status" IN ('Draft', 'Upcoming', 'Closed', 'Cancelled');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260724122144_NormalizeNightMarketOperationalStatus') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260724122144_NormalizeNightMarketOperationalStatus', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726041910_AddZoneGeneratorFields') THEN
    ALTER TABLE "Zones" ADD "Capacity" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726041910_AddZoneGeneratorFields') THEN
    ALTER TABLE "Zones" ADD "DefaultBoothHeight" double precision NOT NULL DEFAULT 60.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726041910_AddZoneGeneratorFields') THEN
    ALTER TABLE "Zones" ADD "DefaultBoothWidth" double precision NOT NULL DEFAULT 80.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726041910_AddZoneGeneratorFields') THEN
    ALTER TABLE "Zones" ADD "DefaultGap" double precision NOT NULL DEFAULT 20.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726041910_AddZoneGeneratorFields') THEN
    ALTER TABLE "Zones" ADD "ZoneCode" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726041910_AddZoneGeneratorFields') THEN
    CREATE UNIQUE INDEX ux_zone_market_code_active ON "Zones" ("NightMarketId", "ZoneCode") WHERE "ZoneCode" IS NOT NULL AND "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726041910_AddZoneGeneratorFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260726041910_AddZoneGeneratorFields', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726044244_BackfillZoneCapacity') THEN

                    UPDATE "Zones"
                    SET "Capacity" = COALESCE((
                        SELECT MAX(SlotCount)
                        FROM (
                            SELECT COUNT(*) AS SlotCount
                            FROM "LayoutNodes"
                            WHERE "NodeType" = 'BoothSlot' AND "ZoneId" = "Zones"."Id"
                            GROUP BY "LayoutId"
                        ) AS Subquery
                    ), 0)
                    WHERE "Capacity" = 0;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726044244_BackfillZoneCapacity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260726044244_BackfillZoneCapacity', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726205516_AddCheckoutIdempotency') THEN
    ALTER TABLE "Order" ADD "CheckoutRequestId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726205516_AddCheckoutIdempotency') THEN
    CREATE UNIQUE INDEX ux_order_customer_checkout_request ON "Order" ("CustomerId", "CheckoutRequestId") WHERE "CheckoutRequestId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726205516_AddCheckoutIdempotency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260726205516_AddCheckoutIdempotency', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726211644_AddRefundPayoutLifecycle') THEN
    ALTER TABLE "Payments" ADD "PayoutId" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726211644_AddRefundPayoutLifecycle') THEN
    ALTER TABLE "Payments" ADD "RefundAmount" numeric(12,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726211644_AddRefundPayoutLifecycle') THEN
    ALTER TABLE "Payments" ADD "RefundReference" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726211644_AddRefundPayoutLifecycle') THEN
    ALTER TABLE "Payments" ADD "RefundRequestedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726211644_AddRefundPayoutLifecycle') THEN
    ALTER TABLE "Payments" ADD "RefundedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726211644_AddRefundPayoutLifecycle') THEN
    CREATE UNIQUE INDEX ux_payments_payout_id ON "Payments" ("PayoutId") WHERE "PayoutId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726211644_AddRefundPayoutLifecycle') THEN
    CREATE UNIQUE INDEX ux_payments_refund_reference ON "Payments" ("RefundReference") WHERE "RefundReference" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260726211644_AddRefundPayoutLifecycle') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260726211644_AddRefundPayoutLifecycle', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727000000_AddPackageImageUrl') THEN
    ALTER TABLE "Package" ADD "ImageUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727000000_AddPackageImageUrl') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727000000_AddPackageImageUrl', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727000000_FixNightMarketStatusDefault') THEN
    UPDATE "NightMarket"
    SET "Status" = CASE
        WHEN "Status" IN ('Active', 'Open', 'Upcoming') THEN 'Active'
        ELSE 'Inactive'
    END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727000000_FixNightMarketStatusDefault') THEN
    ALTER TABLE "NightMarket" ALTER COLUMN "Status" TYPE character varying(20);
    ALTER TABLE "NightMarket" ALTER COLUMN "Status" SET DEFAULT 'Inactive';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727000000_FixNightMarketStatusDefault') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727000000_FixNightMarketStatusDefault', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727010000_AddNightMarketImagesTable') THEN
    CREATE TABLE "NightMarketImages" (
        "Id" uuid NOT NULL DEFAULT (uuid_generate_v4()),
        "NightMarketId" uuid NOT NULL,
        "ImageUrl" character varying(500) NOT NULL,
        "DisplayOrder" integer NOT NULL DEFAULT 0,
        "IsCover" boolean NOT NULL DEFAULT FALSE,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "NightMarketImages_pkey" PRIMARY KEY ("Id"),
        CONSTRAINT "NightMarketImages_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES "NightMarket" ("Id") ON DELETE CASCADE
    );
    COMMENT ON TABLE "NightMarketImages" IS 'Thư viện ảnh (gallery) của chợ đêm';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727010000_AddNightMarketImagesTable') THEN
    CREATE INDEX idx_nightmarketimage_market ON "NightMarketImages" ("NightMarketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727010000_AddNightMarketImagesTable') THEN
    CREATE UNIQUE INDEX ux_nightmarketimage_one_cover ON "NightMarketImages" ("NightMarketId") WHERE "IsCover" = true AND "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727010000_AddNightMarketImagesTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727010000_AddNightMarketImagesTable', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727020000_NormalizeBoothStatus') THEN
    UPDATE "Booth"
    SET "Status" = 'Banned'
    WHERE "Status" = 'Suspended';

    UPDATE "Booth"
    SET "Status" = 'Inactive'
    WHERE "Status" IN ('Pending', 'PendingApproval', 'Closed');

    UPDATE "ModerationActionHistory"
    SET "PreviousStatus" = 'Banned'
    WHERE "BoothId" IS NOT NULL AND "PreviousStatus" = 'Suspended';

    UPDATE "ModerationActionHistory"
    SET "PreviousStatus" = 'Inactive'
    WHERE "BoothId" IS NOT NULL AND "PreviousStatus" IN ('Pending', 'PendingApproval', 'Closed');

    UPDATE "ModerationActionHistory"
    SET "NewStatus" = 'Banned'
    WHERE "BoothId" IS NOT NULL AND "NewStatus" = 'Suspended';

    UPDATE "ModerationActionHistory"
    SET "NewStatus" = 'Inactive'
    WHERE "BoothId" IS NOT NULL AND "NewStatus" IN ('Pending', 'PendingApproval', 'Closed');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727020000_NormalizeBoothStatus') THEN
    ALTER TABLE "Booth" ALTER COLUMN "Status" SET DEFAULT ('Inactive'::character varying);
    COMMENT ON COLUMN "Booth"."Status" IS 'Active | Inactive | Banned';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727020000_NormalizeBoothStatus') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727020000_NormalizeBoothStatus', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727020000_RepairNightMarketStatusValues') THEN
    UPDATE "NightMarket"
    SET "Status" = CASE
        WHEN "Status" IN ('Active', 'Open', 'Upcoming') THEN 'Active'
        ELSE 'Inactive'
    END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727020000_RepairNightMarketStatusValues') THEN
    ALTER TABLE "NightMarket" ALTER COLUMN "Status" TYPE character varying(20);
    ALTER TABLE "NightMarket" ALTER COLUMN "Status" SET DEFAULT 'Inactive';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727020000_RepairNightMarketStatusValues') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727020000_RepairNightMarketStatusValues', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727022959_AddPayoutDispatchClaim') THEN
    ALTER TABLE "Payments" ADD "PayoutCreateClaimedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727022959_AddPayoutDispatchClaim') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727022959_AddPayoutDispatchClaim', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727030000_AddModerationHistoryComplaintId') THEN
    ALTER TABLE "ModerationActionHistory" ADD COLUMN IF NOT EXISTS "ComplaintId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727030000_AddModerationHistoryComplaintId') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727030000_AddModerationHistoryComplaintId', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    ALTER TABLE "Conversations" DROP CONSTRAINT "Conversations_BoothOwnerId_fkey";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    DROP INDEX uq_conversation_customer_boothowner;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    DROP INDEX "IX_Conversations_BoothOwnerId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    ALTER TABLE "Conversations" ADD "BoothId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    UPDATE "Conversations" AS conversation
    SET "BoothId" = booth."Id"
    FROM "Booth" AS booth
    WHERE booth."BoothOwnerId" = conversation."BoothOwnerId";

    DO $$
    BEGIN
        IF EXISTS (SELECT 1 FROM "Conversations" WHERE "BoothId" IS NULL) THEN
            RAISE EXCEPTION 'Cannot anchor every existing conversation to a booth.';
        END IF;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    ALTER TABLE "Conversations" ALTER COLUMN "BoothId" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    ALTER TABLE "Conversations" DROP COLUMN "BoothOwnerId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    CREATE INDEX "IX_Conversations_BoothId" ON "Conversations" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    CREATE UNIQUE INDEX uq_conversation_customer_booth ON "Conversations" ("CustomerId", "BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    ALTER TABLE "Conversations" ADD CONSTRAINT "Conversations_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727033511_AnchorChatConversationsToBooth') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727033511_AnchorChatConversationsToBooth', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727034827_OptimizeChatNotificationIndexes') THEN
    DROP INDEX idx_message_conversation;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727034827_OptimizeChatNotificationIndexes') THEN
    DROP INDEX "IX_Conversations_BoothId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727034827_OptimizeChatNotificationIndexes') THEN
    CREATE INDEX idx_notification_user_read ON "Notification" ("UserId", "IsRead", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727034827_OptimizeChatNotificationIndexes') THEN
    CREATE INDEX idx_message_conversation ON "Message" ("ConversationId", "CreatedAt", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727034827_OptimizeChatNotificationIndexes') THEN
    CREATE INDEX idx_conversation_booth_last_message ON "Conversations" ("BoothId", "LastMessageAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727034827_OptimizeChatNotificationIndexes') THEN
    CREATE INDEX idx_conversation_customer_last_message ON "Conversations" ("CustomerId", "LastMessageAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727034827_OptimizeChatNotificationIndexes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727034827_OptimizeChatNotificationIndexes', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727040000_AddBoothIdToBoothDocuments') THEN
    ALTER TABLE "BoothDocuments" ADD COLUMN IF NOT EXISTS "BoothId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727040000_AddBoothIdToBoothDocuments') THEN
    CREATE INDEX IF NOT EXISTS "IX_BoothDocuments_BoothId" ON "BoothDocuments" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727040000_AddBoothIdToBoothDocuments') THEN
    ALTER TABLE "BoothDocuments" DROP CONSTRAINT IF EXISTS "FK_BoothDocuments_Booth_BoothId";
    ALTER TABLE "BoothDocuments"
        ADD CONSTRAINT "FK_BoothDocuments_Booth_BoothId"
        FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727040000_AddBoothIdToBoothDocuments') THEN
    ALTER TABLE "BoothDocuments" ALTER COLUMN "RegistrationId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727040000_AddBoothIdToBoothDocuments') THEN
    UPDATE "BoothDocuments" SET "VerificationStatus" = 'PendingReview' WHERE "VerificationStatus" = 'Pending';
    ALTER TABLE "BoothDocuments" ALTER COLUMN "VerificationStatus" SET DEFAULT 'PendingReview';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727040000_AddBoothIdToBoothDocuments') THEN
    UPDATE "BoothDocuments" bd
    SET "BoothId" = b."Id"
    FROM "Booth" b
    WHERE b."RegistrationId" = bd."RegistrationId"
      AND bd."BoothId" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727040000_AddBoothIdToBoothDocuments') THEN
    ALTER TABLE "Booth" ADD COLUMN IF NOT EXISTS "LogoUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727040000_AddBoothIdToBoothDocuments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727040000_AddBoothIdToBoothDocuments', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    DROP INDEX "IX_Reviews_CustomerId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    DROP INDEX "IX_Complaints_CustomerId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    ALTER TABLE "PromotionUsages" ADD "PromotionCodeSnapshot" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    ALTER TABLE "PromotionUsages" ADD "PromotionTitleSnapshot" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    ALTER TABLE "OrderDetail" ADD "FoodNameSnapshot" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    UPDATE "OrderDetail" AS od
    SET "FoodNameSnapshot" = COALESCE(NULLIF(f."Name", ''), 'Unknown item')
    FROM "FoodItem" AS f
    WHERE od."FoodItemId" = f."Id";

    UPDATE "OrderDetail"
    SET "FoodNameSnapshot" = 'Unknown item'
    WHERE "FoodNameSnapshot" IS NULL;

    UPDATE "PromotionUsages" AS pu
    SET "PromotionCodeSnapshot" = p."PromotionCode",
        "PromotionTitleSnapshot" = COALESCE(NULLIF(p."Title", ''), 'Legacy promotion')
    FROM "Promotion" AS p
    WHERE pu."PromotionId" = p."Id";

    UPDATE "PromotionUsages"
    SET "PromotionTitleSnapshot" = 'Legacy promotion'
    WHERE "PromotionTitleSnapshot" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    ALTER TABLE "PromotionUsages" ALTER COLUMN "PromotionTitleSnapshot" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    ALTER TABLE "OrderDetail" ALTER COLUMN "FoodNameSnapshot" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    CREATE INDEX idx_reviews_booth_visible_created ON "Reviews" ("BoothId", "IsVisible", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    CREATE INDEX idx_reviews_customer_created ON "Reviews" ("CustomerId", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    ALTER TABLE "Reviews" ADD CONSTRAINT ck_reviews_rating CHECK ("Rating" BETWEEN 1 AND 5);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    UPDATE "Booth" AS b
    SET "AverageRating" = COALESCE((
        SELECT ROUND(AVG(r."Rating"::numeric), 2)
        FROM "Reviews" AS r
        WHERE r."BoothId" = b."Id" AND r."IsVisible" = TRUE
    ), 0)
    WHERE EXISTS (
        SELECT 1 FROM "Reviews" AS existing WHERE existing."BoothId" = b."Id"
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    CREATE INDEX idx_order_customer_created ON "Order" ("CustomerId", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    CREATE INDEX idx_order_customer_status_created ON "Order" ("CustomerId", "Status", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    CREATE INDEX idx_complaint_customer_created ON "Complaints" ("CustomerId", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    CREATE UNIQUE INDEX uq_complaint_active_customer_order_booth ON "Complaints" ("CustomerId", "OrderId", "BoothId") WHERE "Status" = 'Pending';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727044112_CompleteCustomerHistoryReviewComplaint') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727044112_CompleteCustomerHistoryReviewComplaint', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727110000_RemoveDraftFromMarketLayoutStatus') THEN
    UPDATE "MarketLayouts" SET "Status" = 'Inactive' WHERE "Status" = 'Draft';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727110000_RemoveDraftFromMarketLayoutStatus') THEN
    ALTER TABLE "MarketLayouts" ALTER COLUMN "Status" TYPE character varying(20);
    ALTER TABLE "MarketLayouts" ALTER COLUMN "Status" SET DEFAULT ('Inactive'::character varying);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727110000_RemoveDraftFromMarketLayoutStatus') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727110000_RemoveDraftFromMarketLayoutStatus', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727160000_RepairBoothStatusDomainValues') THEN
    UPDATE "Booth"
    SET "Status" = CASE
        WHEN "Status" = 'Suspended' THEN 'Banned'
        WHEN "Status" IN ('Active', 'Inactive', 'Banned') THEN "Status"
        ELSE 'Inactive'
    END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727160000_RepairBoothStatusDomainValues') THEN
    ALTER TABLE "Booth" ALTER COLUMN "Status" TYPE character varying(20);
    ALTER TABLE "Booth" ALTER COLUMN "Status" DROP DEFAULT;
    COMMENT ON COLUMN "Booth"."Status" IS 'Active | Inactive | Banned';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727160000_RepairBoothStatusDomainValues') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727160000_RepairBoothStatusDomainValues', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" DROP CONSTRAINT "FoodCategories_BoothId_fkey";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" ALTER COLUMN "BoothId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" ADD "Code" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" ADD "DisplayOrder" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" ADD "IsActive" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" ADD "IsSelectable" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" ADD "IsSystem" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    UPDATE "FoodCategories"
    SET "Code" = 'LEGACY_' || upper(replace("Id"::text, '-', ''))
    WHERE "Code" IS NULL OR btrim("Code") = '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" ALTER COLUMN "Code" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodTag" ADD "DisplayOrder" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodTag" ADD "IsAutoAssigned" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodTag" ADD "IsPreferenceSelectable" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodTag" ADD "IsSelectable" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodTag" ADD "IsSystem" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    CREATE UNIQUE INDEX ux_foodcategory_code_active ON "FoodCategories" ("Code") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    ALTER TABLE "FoodCategories" ADD CONSTRAINT "FoodCategories_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260728190517_AddSystemFoodTaxonomy') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260728190517_AddSystemFoodTaxonomy', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Payments" ADD "CancelledAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Payments" ADD "ExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Payments" ADD "FailedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Payments" ADD "FailureCode" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Payments" ADD "FailureMessage" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Payments" ADD "QrCode" character varying(4000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Payments" ADD "RowVersion" bytea NOT NULL DEFAULT BYTEA E'\\x';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "BoothId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "CancellationReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "CancelledAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "CompletedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "IdempotencyKey" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "PaymentMethod" character varying(20) NOT NULL DEFAULT 'Cash';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "PromotionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "PromotionSnapshot" jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "RequestHash" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD "RowVersion" bytea NOT NULL DEFAULT BYTEA E'\\x';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    UPDATE "Order" AS o
    SET "BoothId" = source."BoothId"
    FROM (
        SELECT od."OrderId", MIN(fi."BoothId"::text)::uuid AS "BoothId"
        FROM "OrderDetail" AS od
        INNER JOIN "FoodItem" AS fi ON fi."Id" = od."FoodItemId"
        GROUP BY od."OrderId"
    ) AS source
    WHERE source."OrderId" = o."Id";

    UPDATE "Order" AS o
    SET "PaymentMethod" = COALESCE(
        (SELECT p."Type" FROM "Payments" AS p WHERE p."OrderId" = o."Id" ORDER BY p."CreatedAt" LIMIT 1),
        'Cash');

    UPDATE "Order"
    SET "IdempotencyKey" = "CheckoutRequestId"::text
    WHERE "CheckoutRequestId" IS NOT NULL;

    DO $$
    BEGIN
        IF EXISTS (SELECT 1 FROM "Order" WHERE "BoothId" IS NULL) THEN
            RAISE EXCEPTION 'Cannot migrate orders without a booth derived from their order details.';
        END IF;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ALTER COLUMN "BoothId" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    CREATE TABLE "PaymentAttempts" (
        "Id" uuid NOT NULL,
        "PaymentId" uuid NOT NULL,
        "AttemptNumber" integer NOT NULL,
        "ProviderOrderCode" bigint NOT NULL,
        "ProviderPaymentLinkId" character varying(255),
        "Status" character varying(20) NOT NULL,
        "CheckoutUrl" character varying(2000),
        "QrCode" character varying(4000),
        "FailureCode" character varying(100),
        "FailureMessage" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone,
        CONSTRAINT "PK_PaymentAttempts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PaymentAttempts_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES "Payments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    CREATE TABLE "PaymentWebhookEvents" (
        "Id" uuid NOT NULL,
        "Provider" character varying(30) NOT NULL,
        "ProviderEventKey" character varying(255) NOT NULL,
        "OrderCode" bigint NOT NULL,
        "SignatureHash" character varying(64) NOT NULL,
        "PayloadHash" character varying(64) NOT NULL,
        "ProcessingStatus" character varying(20) NOT NULL,
        "ReceivedAt" timestamp with time zone NOT NULL,
        "ProcessedAt" timestamp with time zone,
        "Error" character varying(500),
        CONSTRAINT "PK_PaymentWebhookEvents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    INSERT INTO "PaymentAttempts"
        ("Id", "PaymentId", "AttemptNumber", "ProviderOrderCode", "ProviderPaymentLinkId",
         "Status", "CheckoutUrl", "QrCode", "CreatedAt", "UpdatedAt", "ExpiresAt")
    SELECT uuid_generate_v4(), p."Id", 1, p."PayOSOrderCode", p."PaymentLinkId",
           CASE p."Status"
               WHEN 'Paid' THEN 'Paid'
               WHEN 'Cancelled' THEN 'Cancelled'
               WHEN 'Failed' THEN 'Failed'
               ELSE 'Pending'
           END,
           p."CheckoutUrl", p."QrCode", p."CreatedAt", p."UpdatedAt", p."ExpiresAt"
    FROM "Payments" AS p
    WHERE p."PayOSOrderCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    CREATE INDEX "IX_Order_BoothId" ON "Order" ("BoothId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    CREATE UNIQUE INDEX ux_order_customer_idempotency_key ON "Order" ("CustomerId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    CREATE UNIQUE INDEX "IX_PaymentAttempts_PaymentId_AttemptNumber" ON "PaymentAttempts" ("PaymentId", "AttemptNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    CREATE UNIQUE INDEX "IX_PaymentAttempts_ProviderOrderCode" ON "PaymentAttempts" ("ProviderOrderCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    CREATE UNIQUE INDEX "IX_PaymentWebhookEvents_Provider_PayloadHash" ON "PaymentWebhookEvents" ("Provider", "PayloadHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    CREATE UNIQUE INDEX "IX_PaymentWebhookEvents_Provider_ProviderEventKey" ON "PaymentWebhookEvents" ("Provider", "ProviderEventKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    ALTER TABLE "Order" ADD CONSTRAINT "Order_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729113011_RebuildCustomerOrderPaymentFlow') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260729113011_RebuildCustomerOrderPaymentFlow', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729114936_AddCheckoutCartSnapshot') THEN
    ALTER TABLE "Order" ADD "CheckoutCartItemIds" jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729114936_AddCheckoutCartSnapshot') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260729114936_AddCheckoutCartSnapshot', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729121302_FixOrderPaymentConcurrencyTokens') THEN
    ALTER TABLE "Payments" ALTER COLUMN "RowVersion" SET DEFAULT (uuid_send(gen_random_uuid()));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729121302_FixOrderPaymentConcurrencyTokens') THEN
    ALTER TABLE "Order" ALTER COLUMN "RowVersion" SET DEFAULT (uuid_send(gen_random_uuid()));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729121302_FixOrderPaymentConcurrencyTokens') THEN
    CREATE OR REPLACE FUNCTION public.snm_set_order_payment_row_version()
    RETURNS trigger
    LANGUAGE plpgsql
    AS $$
    BEGIN
        NEW."RowVersion" := uuid_send(gen_random_uuid());
        RETURN NEW;
    END;
    $$;

    UPDATE "Order" SET "RowVersion" = uuid_send(gen_random_uuid());
    UPDATE "Payments" SET "RowVersion" = uuid_send(gen_random_uuid());

    CREATE TRIGGER trg_order_row_version
    BEFORE UPDATE ON "Order"
    FOR EACH ROW EXECUTE FUNCTION public.snm_set_order_payment_row_version();

    CREATE TRIGGER trg_payments_row_version
    BEFORE UPDATE ON "Payments"
    FOR EACH ROW EXECUTE FUNCTION public.snm_set_order_payment_row_version();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260729121302_FixOrderPaymentConcurrencyTokens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260729121302_FixOrderPaymentConcurrencyTokens', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "MarketLayouts" ADD "MarketWidthMeters" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "MarketLayouts" ADD "MarketLengthMeters" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "MarketLayouts" ADD "PixelsPerMeter" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "Zones" ADD "WidthMeters" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "Zones" ADD "LengthMeters" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "Zones" ADD "BoothWidthMeters" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "Zones" ADD "BoothLengthMeters" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "Zones" ADD "HorizontalGapMeters" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    ALTER TABLE "Zones" ADD "VerticalGapMeters" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730090000_AddPhysicalLayoutGeometry') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260730090000_AddPhysicalLayoutGeometry', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260801090000_AddSupportTickets') THEN
    CREATE TABLE IF NOT EXISTS "SupportTickets" (
        "Id" uuid NOT NULL,
        "TicketCode" character varying(24) NOT NULL,
        "RequesterId" uuid NOT NULL,
        "RequesterRole" character varying(30) NOT NULL,
        "BoothId" uuid NULL,
        "NightMarketId" uuid NULL,
        "Category" character varying(50) NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Description" character varying(4000) NOT NULL,
        "Status" character varying(30) NOT NULL,
        "Priority" character varying(20) NOT NULL,
        "PageUrl" character varying(1000) NULL,
        "AssignedAdminId" uuid NULL,
        "DueAt" timestamp with time zone NOT NULL,
        "FirstRespondedAt" timestamp with time zone NULL,
        "ResolvedAt" timestamp with time zone NULL,
        "ClosedAt" timestamp with time zone NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SupportTickets" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupportTickets_User_RequesterId" FOREIGN KEY ("RequesterId") REFERENCES "User" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupportTickets_User_AssignedAdminId" FOREIGN KEY ("AssignedAdminId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_SupportTickets_TicketCode" ON "SupportTickets" ("TicketCode");
    CREATE INDEX IF NOT EXISTS "IX_SupportTickets_RequesterId_CreatedAt" ON "SupportTickets" ("RequesterId", "CreatedAt");
    CREATE INDEX IF NOT EXISTS "IX_SupportTickets_Status_DueAt" ON "SupportTickets" ("Status", "DueAt");

    CREATE TABLE IF NOT EXISTS "SupportMessages" (
        "Id" uuid NOT NULL,
        "TicketId" uuid NOT NULL,
        "SenderId" uuid NOT NULL,
        "SenderRole" character varying(30) NOT NULL,
        "Body" character varying(4000) NOT NULL,
        "IsInternalNote" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SupportMessages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupportMessages_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "SupportTickets" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SupportMessages_User_SenderId" FOREIGN KEY ("SenderId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    CREATE INDEX IF NOT EXISTS "IX_SupportMessages_TicketId" ON "SupportMessages" ("TicketId");

    CREATE TABLE IF NOT EXISTS "SupportAttachments" (
        "Id" uuid NOT NULL,
        "TicketId" uuid NOT NULL,
        "MessageId" uuid NULL,
        "FileUrl" character varying(1000) NOT NULL,
        "OriginalFileName" character varying(255) NOT NULL,
        "ContentType" character varying(100) NOT NULL,
        "FileSize" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SupportAttachments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupportAttachments_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "SupportTickets" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SupportAttachments_SupportMessages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "SupportMessages" ("Id") ON DELETE SET NULL
    );
    CREATE INDEX IF NOT EXISTS "IX_SupportAttachments_TicketId" ON "SupportAttachments" ("TicketId");
    CREATE INDEX IF NOT EXISTS "IX_SupportAttachments_MessageId" ON "SupportAttachments" ("MessageId");

    CREATE TABLE IF NOT EXISTS "SupportStatusHistories" (
        "Id" uuid NOT NULL,
        "TicketId" uuid NOT NULL,
        "ActorId" uuid NOT NULL,
        "FromStatus" character varying(30) NULL,
        "ToStatus" character varying(30) NOT NULL,
        "Note" character varying(1000) NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SupportStatusHistories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupportStatusHistories_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "SupportTickets" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SupportStatusHistories_User_ActorId" FOREIGN KEY ("ActorId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    CREATE INDEX IF NOT EXISTS "IX_SupportStatusHistories_TicketId" ON "SupportStatusHistories" ("TicketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260801090000_AddSupportTickets') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260801090000_AddSupportTickets', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260803072406_ReconcileDevelopmentSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260803072406_ReconcileDevelopmentSchema', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260803073753_ReconcileModerationSchema') THEN
    DO $$
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM "ModerationActionHistory"
            WHERE "AdminId" IS NULL
               OR "PreviousStatus" IS NULL
               OR "NewStatus" IS NULL
               OR "Reason" IS NULL
        ) THEN
            RAISE EXCEPTION 'Cannot reconcile ModerationActionHistory: required fields contain NULL values.';
        END IF;
    END $$;

    ALTER TABLE "ModerationActionHistory"
        ALTER COLUMN "AdminId" SET NOT NULL,
        ALTER COLUMN "PreviousStatus" SET NOT NULL,
        ALTER COLUMN "NewStatus" SET NOT NULL,
        ALTER COLUMN "Reason" SET NOT NULL;

    DO $$
    BEGIN
        IF EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conrelid = '"ModerationActionHistory"'::regclass
              AND conname = 'PK_ModerationActionHistory'
        ) AND NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conrelid = '"ModerationActionHistory"'::regclass
              AND conname = 'ModerationActionHistory_pkey'
        ) THEN
            ALTER TABLE "ModerationActionHistory"
                RENAME CONSTRAINT "PK_ModerationActionHistory" TO "ModerationActionHistory_pkey";
        END IF;
    END $$;

    CREATE INDEX IF NOT EXISTS "idx_nightmarket_moderation_status"
        ON "NightMarket" ("ModerationStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260803073753_ReconcileModerationSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260803073753_ReconcileModerationSchema', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260803073914_RemoveConfirmedLegacyNightMarketColumns') THEN
    DO $$
    DECLARE
        has_data boolean;
        legacy_column text;
    BEGIN
        FOREACH legacy_column IN ARRAY ARRAY[
            'ModerationNotes',
            'RejectedReason',
            'LiveStreamUrl',
            'LiveStreamStartedAt',
            'LiveStreamEndedAt'
        ]
        LOOP
            IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'NightMarket'
                  AND information_schema.columns.column_name = legacy_column
            ) THEN
                EXECUTE format(
                    'SELECT EXISTS (SELECT 1 FROM "NightMarket" WHERE %I IS NOT NULL)',
                    legacy_column
                ) INTO has_data;
                IF has_data THEN
                    RAISE EXCEPTION 'Legacy NightMarket column % contains data. Cleanup aborted.', legacy_column;
                END IF;
            END IF;
        END LOOP;

        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'NightMarket'
              AND information_schema.columns.column_name = 'HasLiveStream'
        ) THEN
            EXECUTE 'SELECT EXISTS (SELECT 1 FROM "NightMarket" WHERE "HasLiveStream" = true)'
                INTO has_data;
            IF has_data THEN
                RAISE EXCEPTION 'Legacy NightMarket column HasLiveStream contains active data. Cleanup aborted.';
            END IF;
        END IF;
    END $$;

    ALTER TABLE "NightMarket"
        DROP COLUMN IF EXISTS "ModerationNotes",
        DROP COLUMN IF EXISTS "RejectedReason",
        DROP COLUMN IF EXISTS "HasLiveStream",
        DROP COLUMN IF EXISTS "LiveStreamUrl",
        DROP COLUMN IF EXISTS "LiveStreamStartedAt",
        DROP COLUMN IF EXISTS "LiveStreamEndedAt";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260803073914_RemoveConfirmedLegacyNightMarketColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260803073914_RemoveConfirmedLegacyNightMarketColumns', '8.0.28');
    END IF;
END $EF$;
COMMIT;

