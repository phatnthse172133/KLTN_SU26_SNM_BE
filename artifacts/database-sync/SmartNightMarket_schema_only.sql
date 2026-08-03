--
-- PostgreSQL database dump
--

\restrict gfGdITz0wZW72fpsWOUgA8HjSKCwxQn4qgi89N7veIRA1TMVbIvYNmkrtkoMUMD

-- Dumped from database version 18.1
-- Dumped by pg_dump version 18.1

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: uuid-ossp; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA public;


--
-- Name: EXTENSION "uuid-ossp"; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION "uuid-ossp" IS 'generate universally unique identifiers (UUIDs)';


--
-- Name: snm_set_order_payment_row_version(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.snm_set_order_payment_row_version() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    NEW."RowVersion" := uuid_send(gen_random_uuid());
    RETURN NEW;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: AIRecommendationLog; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AIRecommendationLog" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "CustomerId" uuid,
    "NightMarketId" uuid,
    "RecommendationType" character varying(30) NOT NULL,
    "InputJson" jsonb NOT NULL,
    "ParsedIntentJson" jsonb,
    "ResultJson" jsonb NOT NULL,
    "SelectedOptionId" character varying(100),
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "AIRecommendationLog"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."AIRecommendationLog" IS 'Log tối giản cho các lần AI recommendation để debug/demo';


--
-- Name: Booth; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Booth" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "RegistrationId" uuid,
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
    "AverageRating" numeric(3,2) DEFAULT 0,
    "IsFeatured" boolean DEFAULT false NOT NULL,
    "PackageName" character varying(100),
    "PackageExpiryDate" timestamp with time zone,
    "PaymentQRImage" text,
    "Status" character varying(20) NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "LogoUrl" character varying(500)
);


--
-- Name: TABLE "Booth"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Booth" IS 'Gian hàng ẩm thực - thực thể trung tâm, mỗi gian hàng thuộc 1 NightMarket và do 1 User (BoothOwner) quản lý';


--
-- Name: COLUMN "Booth"."AverageRating"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Booth"."AverageRating" IS 'Cache điểm trung bình review, cập nhật qua trigger hoặc job định kỳ';


--
-- Name: COLUMN "Booth"."Status"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Booth"."Status" IS 'Active | Inactive | Banned';


--
-- Name: BoothDocuments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BoothDocuments" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "RegistrationId" uuid,
    "DocumentType" text NOT NULL,
    "DocumentUrl" text NOT NULL,
    "FileUrl" character varying(500) NOT NULL,
    "VerificationStatus" character varying(20) DEFAULT 'PendingReview'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "BoothId" uuid
);


--
-- Name: TABLE "BoothDocuments"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."BoothDocuments" IS 'Giấy tờ pháp lý của gian hàng để Admin xác minh trước khi cho phép hoạt động';


--
-- Name: BoothImages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BoothImages" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "BoothId" uuid NOT NULL,
    "ImageUrl" character varying(500) NOT NULL,
    "DisplayOrder" integer DEFAULT 0 NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "BoothImages"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."BoothImages" IS 'Thư viện ảnh (gallery) của gian hàng';


--
-- Name: BoothLocations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BoothLocations" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "BoothId" uuid NOT NULL,
    "LayoutId" uuid NOT NULL,
    "XCoordinate" numeric(10,2) NOT NULL,
    "YCoordinate" numeric(10,2) NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "LayoutNodeId" uuid NOT NULL,
    "ReleasedAt" timestamp with time zone,
    "SlotNumber" character varying(50),
    "ZoneId" uuid
);


--
-- Name: TABLE "BoothLocations"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."BoothLocations" IS 'Vị trí cụ thể (tọa độ) của 1 gian hàng trên 1 sơ đồ mặt bằng';


--
-- Name: BoothPaymentInfos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BoothPaymentInfos" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "BoothId" uuid NOT NULL,
    "PaymentType" integer NOT NULL,
    "BankName" character varying(100),
    "BankAccountNumber" character varying(50),
    "BankAccountHolder" character varying(150),
    "QRImageUrl" character varying(500),
    "IsDefault" boolean DEFAULT false NOT NULL,
    "Status" character varying(20) DEFAULT 'Draft'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "BoothPaymentInfos"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."BoothPaymentInfos" IS 'Thông tin tài khoản/QR nhận thanh toán của gian hàng';


--
-- Name: COLUMN "BoothPaymentInfos"."PaymentType"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."BoothPaymentInfos"."PaymentType" IS 'BankTransfer | VNPay | MoMo | ZaloPay | Payos';


--
-- Name: BoothRegistrations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BoothRegistrations" (
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
    "BoothId" uuid
);


--
-- Name: BoothSubscriptions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BoothSubscriptions" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "BoothId" uuid NOT NULL,
    "PackageId" uuid NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "AdminNotes" text,
    "PaymentEvidenceUrl" text,
    "PaidAmount" numeric(18,2) DEFAULT 0.0 NOT NULL,
    "PaidAt" timestamp with time zone,
    "PayOSOrderCode" bigint,
    "PayOSPaymentLinkId" character varying(100),
    "PaymentExpiresAt" timestamp with time zone,
    "ChangeType" text,
    "CreditAmount" numeric DEFAULT 0.0 NOT NULL,
    "PausedAt" timestamp with time zone,
    "PausedRemainingDays" integer,
    "PolicyAcceptedAt" timestamp with time zone,
    "PolicySnapshotJson" text,
    "PolicyVersion" text,
    "PreviousSubscriptionId" uuid,
    "BuyerName" text,
    "BuyerEmail" text,
    "BuyerPhone" text
);


--
-- Name: TABLE "BoothSubscriptions"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."BoothSubscriptions" IS 'Lịch sử đăng ký gói dịch vụ của gian hàng';


--
-- Name: COLUMN "BoothSubscriptions"."Status"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."BoothSubscriptions"."Status" IS 'Active | Expired | Cancelled';


--
-- Name: Cart; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Cart" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "CustomerId" uuid NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "Cart"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Cart" IS 'Giỏ hàng hiện tại của khách hàng';


--
-- Name: CartItem; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CartItem" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "CartId" uuid NOT NULL,
    "FoodItemId" uuid NOT NULL,
    "Quantity" integer NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "CartItem"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."CartItem" IS 'Món ăn trong giỏ hàng';


--
-- Name: ComplaintImages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ComplaintImages" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "ComplaintId" uuid NOT NULL,
    "ImageUrl" character varying(500) NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "ComplaintImages"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."ComplaintImages" IS 'Ảnh minh chứng đính kèm theo khiếu nại';


--
-- Name: Complaints; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Complaints" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "CustomerId" uuid NOT NULL,
    "BoothId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" text NOT NULL,
    "AdminResponse" text,
    "Status" character varying(20) DEFAULT 'Pending'::character varying NOT NULL,
    "ResolutionAction" character varying(30),
    "PolicyViolation" character varying(500),
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "Complaints"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Complaints" IS 'Khiếu nại của khách hàng về đơn hàng/gian hàng';


--
-- Name: COLUMN "Complaints"."Status"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Complaints"."Status" IS 'Pending | Resolved | Rejected';


--
-- Name: COLUMN "Complaints"."ResolutionAction"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Complaints"."ResolutionAction" IS 'NoViolation | Warning | SuspendBooth | CloseBooth';


--
-- Name: Conversations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Conversations" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "CustomerId" uuid NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "BoothOwnerLastReadAt" timestamp with time zone,
    "CustomerLastReadAt" timestamp with time zone,
    "LastMessageAt" timestamp with time zone,
    "LastMessageId" uuid,
    "BoothId" uuid NOT NULL
);


--
-- Name: TABLE "Conversations"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Conversations" IS 'Cuộc trò chuyện giữa 1 khách hàng và 1 gian hàng - dùng SignalR để realtime';


--
-- Name: CustomerPreference; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CustomerPreference" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "CustomerId" uuid NOT NULL,
    "FoodTagId" uuid NOT NULL,
    "PreferenceKind" character varying(20) NOT NULL,
    "PreferenceSource" character varying(30) NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "CustomerPreference"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."CustomerPreference" IS 'Sở thích rõ ràng của khách hàng theo FoodTag: Like/Avoid';


--
-- Name: EmailOutbox; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."EmailOutbox" (
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
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: FoodCategories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FoodCategories" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "BoothId" uuid,
    "Name" character varying(100) NOT NULL,
    "Description" text,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "Code" character varying(100) NOT NULL,
    "DisplayOrder" integer DEFAULT 0 NOT NULL,
    "IsActive" boolean DEFAULT true NOT NULL,
    "IsSelectable" boolean DEFAULT true NOT NULL,
    "IsSystem" boolean DEFAULT false NOT NULL
);


--
-- Name: TABLE "FoodCategories"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."FoodCategories" IS 'Danh mục món ăn của từng gian hàng';


--
-- Name: FoodImages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FoodImages" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "FoodItemId" uuid NOT NULL,
    "ImageUrl" character varying(500) NOT NULL,
    "DisplayOrder" integer DEFAULT 0 NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "FoodImages"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."FoodImages" IS 'Thư viện ảnh (gallery) cho từng món ăn';


--
-- Name: FoodItem; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FoodItem" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "BoothId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" text,
    "Price" numeric(12,2) NOT NULL,
    "ThumbnailUrl" character varying(500),
    "IsAvailable" boolean DEFAULT true NOT NULL,
    "IsFeatured" boolean DEFAULT false NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "FoodItem"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."FoodItem" IS 'Món ăn của từng gian hàng';


--
-- Name: COLUMN "FoodItem"."Price"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."FoodItem"."Price" IS 'Giá mặc định. Nếu có FoodPrice theo ngày hiện tại thì giá đó được ưu tiên (override)';


--
-- Name: COLUMN "FoodItem"."IsAvailable"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."FoodItem"."IsAvailable" IS 'false khi món hết nguyên liệu hoặc chủ quán tạm ẩn';


--
-- Name: FoodItemTag; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FoodItemTag" (
    "FoodItemId" uuid NOT NULL,
    "FoodTagId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "FoodItemTag"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."FoodItemTag" IS 'Bảng nối gắn tag ngữ nghĩa vào món ăn';


--
-- Name: FoodPrice; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FoodPrice" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "FoodItemId" uuid NOT NULL,
    "Price" numeric(12,2) NOT NULL,
    "StartDate" timestamp with time zone,
    "EndDate" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL
);


--
-- Name: TABLE "FoodPrice"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."FoodPrice" IS 'Bảng giá theo ngày trong tuần - override giá mặc định của FoodItem';


--
-- Name: FoodTag; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FoodTag" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Code" character varying(100) NOT NULL,
    "Description" text,
    "TagGroup" character varying(30) NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "DisplayOrder" integer DEFAULT 0 NOT NULL,
    "IsAutoAssigned" boolean DEFAULT false NOT NULL,
    "IsPreferenceSelectable" boolean DEFAULT false NOT NULL,
    "IsSelectable" boolean DEFAULT true NOT NULL,
    "IsSystem" boolean DEFAULT false NOT NULL
);


--
-- Name: TABLE "FoodTag"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."FoodTag" IS 'Danh sách tag chuẩn mô tả ngữ nghĩa món ăn cho AI/recommendation';


--
-- Name: LayoutBlocks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."LayoutBlocks" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
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
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "LayoutBlocks"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."LayoutBlocks" IS 'Nhóm các block layout (dãy gian hàng, lối đi, khu vực, v.v.)';


--
-- Name: LayoutEdges; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."LayoutEdges" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "FromNodeId" uuid NOT NULL,
    "ToNodeId" uuid NOT NULL,
    "Distance" numeric(10,2) NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsAccessible" boolean DEFAULT true NOT NULL,
    "IsBidirectional" boolean DEFAULT true NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "LayoutId" uuid NOT NULL
);


--
-- Name: TABLE "LayoutEdges"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."LayoutEdges" IS 'Cạnh nối giữa 2 LayoutNode - thể hiện đường đi và khoảng cách, dùng cho thuật toán tìm đường ngắn nhất trong chợ';


--
-- Name: LayoutNodes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."LayoutNodes" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "LayoutId" uuid NOT NULL,
    "NodeName" character varying(100),
    "XCoordinate" numeric(10,2) NOT NULL,
    "YCoordinate" numeric(10,2) NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsAccessible" boolean DEFAULT true NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "IsStartingPoint" boolean DEFAULT false NOT NULL,
    "NodeType" character varying(20) DEFAULT 'Junction'::character varying NOT NULL,
    "ZoneId" uuid,
    "ColumnIndex" integer,
    "LayoutBlockId" uuid,
    "RowIndex" integer,
    "SlotCode" character varying(20)
);


--
-- Name: TABLE "LayoutNodes"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."LayoutNodes" IS 'Các điểm/nút (node) trên sơ đồ mặt bằng - là đỉnh của đồ thị dùng cho tìm đường nội bộ chợ';


--
-- Name: MarketLayouts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MarketLayouts" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "NightMarketId" uuid NOT NULL,
    "LayoutImageUrl" character varying(500),
    "Width" integer NOT NULL,
    "Height" integer NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "LayoutName" character varying(150) DEFAULT ''::character varying NOT NULL,
    "Status" character varying(20) DEFAULT 'Inactive'::character varying NOT NULL,
    "Version" integer DEFAULT 1 NOT NULL,
    "MarketWidthMeters" double precision,
    "MarketLengthMeters" double precision,
    "PixelsPerMeter" double precision
);


--
-- Name: TABLE "MarketLayouts"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."MarketLayouts" IS 'Sơ đồ mặt bằng của một chợ đêm - dùng làm nền để đặt các điểm (LayoutNodes) và gian hàng (BoothLocations)';


--
-- Name: MarketSubscriptions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MarketSubscriptions" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "MarketOwnerId" uuid NOT NULL,
    "PackageId" uuid NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "PaymentEvidenceUrl" text,
    "AdminNotes" text,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "PaidAmount" numeric(18,2) DEFAULT 0.0 NOT NULL,
    "PaidAt" timestamp with time zone,
    "PayOSOrderCode" bigint,
    "PayOSPaymentLinkId" character varying(100),
    "PaymentExpiresAt" timestamp with time zone,
    "ChangeType" text,
    "CreditAmount" numeric DEFAULT 0.0 NOT NULL,
    "PolicyAcceptedAt" timestamp with time zone,
    "PolicySnapshotJson" text,
    "PolicyVersion" text,
    "PreviousSubscriptionId" uuid,
    "BuyerName" text,
    "BuyerEmail" text,
    "BuyerPhone" text
);


--
-- Name: TABLE "MarketSubscriptions"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."MarketSubscriptions" IS 'Lịch sử đăng ký gói dịch vụ của Market Owner';


--
-- Name: COLUMN "MarketSubscriptions"."Status"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."MarketSubscriptions"."Status" IS 'Active | Expired | Cancelled | PendingPayment';


--
-- Name: Message; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Message" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "ConversationId" uuid NOT NULL,
    "SenderId" uuid NOT NULL,
    "SenderRole" character varying(20) NOT NULL,
    "Type" character varying(20) DEFAULT 'Text'::character varying NOT NULL,
    "Content" text NOT NULL,
    "IsRead" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "ClientMessageId" uuid,
    "DeletedAt" timestamp with time zone,
    "ReadAt" timestamp with time zone
);


--
-- Name: TABLE "Message"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Message" IS 'Tin nhắn trong cuộc trò chuyện - truyền tải qua SignalR Hub';


--
-- Name: COLUMN "Message"."SenderRole"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Message"."SenderRole" IS 'Snapshot vai trò người gửi: Customer | BoothOwner';


--
-- Name: COLUMN "Message"."Type"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Message"."Type" IS 'Text | Image | System';


--
-- Name: ModerationActionHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ModerationActionHistory" (
    "Id" uuid NOT NULL,
    "BoothId" uuid,
    "NightMarketId" uuid,
    "AdminId" uuid NOT NULL,
    "AdminName" character varying(200),
    "PreviousStatus" character varying(20) NOT NULL,
    "NewStatus" character varying(20) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "Source" character varying(20) DEFAULT 'DirectAdmin'::character varying NOT NULL,
    "ComplaintId" uuid,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: NightMarket; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."NightMarket" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" text,
    "Address" character varying(500) NOT NULL,
    "Latitude" numeric(10,7),
    "Longitude" numeric(10,7),
    "OpeningHours" time without time zone,
    "ClosingHours" time without time zone,
    "TotalBooth" integer DEFAULT 0 NOT NULL,
    "BoundaryWidthMeters" integer,
    "BoundaryHeightMeters" integer,
    "ThumbnailUrl" character varying(500),
    "Status" character varying(20) DEFAULT 'Inactive'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "MarketOwnerId" uuid,
    "ModerationStatus" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "DeletedAt" timestamp with time zone,
    "DeletedBy" uuid,
    "DeletionReason" text
);


--
-- Name: TABLE "NightMarket"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."NightMarket" IS 'Thông tin các chợ đêm - đơn vị quản lý cấp cao nhất, chứa nhiều Booth';


--
-- Name: COLUMN "NightMarket"."TotalBooth"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."NightMarket"."TotalBooth" IS 'Số lượng gian hàng - giá trị cache, đồng bộ qua trigger hoặc job định kỳ';


--
-- Name: NightMarketImages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."NightMarketImages" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "NightMarketId" uuid NOT NULL,
    "ImageUrl" character varying(500) NOT NULL,
    "DisplayOrder" integer DEFAULT 0 NOT NULL,
    "IsCover" boolean DEFAULT false NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "NightMarketImages"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."NightMarketImages" IS 'Thư viện ảnh (gallery) của chợ đêm';


--
-- Name: Notification; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Notification" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "UserId" uuid NOT NULL,
    "BoothId" uuid,
    "Type" character varying(50) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Content" text NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "DataJson" text,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "IsRead" boolean DEFAULT false NOT NULL,
    "ReadAt" timestamp with time zone,
    "ReferenceId" uuid,
    "ReferenceType" character varying(100),
    "BatchId" uuid,
    "CreatedByUserId" uuid,
    "Target" integer,
    "TargetRole" character varying(50)
);


--
-- Name: TABLE "Notification"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Notification" IS 'Thông báo đẩy (push notification qua FCM) cho người dùng';


--
-- Name: COLUMN "Notification"."BoothId"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Notification"."BoothId" IS 'NULL khi thông báo không gắn với gian hàng cụ thể (VD: thông báo hệ thống)';


--
-- Name: COLUMN "Notification"."Type"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Notification"."Type" IS 'NewOrder | OrderStatusChanged | NewMessage | Promotion | System';


--
-- Name: Order; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Order" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "CustomerId" uuid NOT NULL,
    "OrderCode" bigint NOT NULL,
    "Status" character varying(30) DEFAULT 'Placed'::character varying NOT NULL,
    "TotalAmount" numeric(12,2) NOT NULL,
    "DiscountAmount" numeric(12,2) NOT NULL,
    "FinalAmount" numeric(12,2) NOT NULL,
    "Note" text,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "BoothOwnerId" uuid DEFAULT '00000000-0000-0000-0000-000000000000'::uuid NOT NULL,
    "PayStatus" integer DEFAULT 0 NOT NULL,
    "CheckoutRequestId" uuid,
    "BoothId" uuid NOT NULL,
    "CancellationReason" character varying(500),
    "CancelledAt" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    "IdempotencyKey" character varying(100),
    "PaymentMethod" character varying(20) DEFAULT 'Cash'::character varying NOT NULL,
    "PromotionId" uuid,
    "PromotionSnapshot" jsonb,
    "RequestHash" character varying(64),
    "RowVersion" bytea DEFAULT uuid_send(gen_random_uuid()) NOT NULL,
    "CheckoutCartItemIds" jsonb
);


--
-- Name: TABLE "Order"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Order" IS 'Đơn hàng của khách (1 đơn chỉ thuộc về 1 quán)';


--
-- Name: COLUMN "Order"."Status"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Order"."Status" IS 'Placed | Preparing | ReadyForPickup | Completed | Cancelled';


--
-- Name: COLUMN "Order"."FinalAmount"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Order"."FinalAmount" IS 'TotalAmount - DiscountAmount';


--
-- Name: OrderDetail; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OrderDetail" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "OrderId" uuid NOT NULL,
    "FoodItemId" uuid NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitPrice" numeric(12,2) NOT NULL,
    "TotalPrice" numeric(12,2) NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "FoodNameSnapshot" character varying(200) NOT NULL
);


--
-- Name: TABLE "OrderDetail"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."OrderDetail" IS 'Chi tiết món ăn trong từng đơn hàng';


--
-- Name: COLUMN "OrderDetail"."UnitPrice"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."OrderDetail"."UnitPrice" IS 'SNAPSHOT giá tại thời điểm đặt hàng - KHÔNG tính lại từ FoodItem.Price';


--
-- Name: Package; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Package" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "PackageName" character varying(100) NOT NULL,
    "Price" numeric(12,2) NOT NULL,
    "DurationDays" integer NOT NULL,
    "Description" text,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "Type" integer DEFAULT 0 NOT NULL,
    "Code" character varying(50),
    "Entitlements" jsonb,
    "ImageUrl" character varying(500)
);


--
-- Name: PackagePolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PackagePolicies" (
    "Id" uuid NOT NULL,
    "PackageId" uuid NOT NULL,
    "Version" character varying(50) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "ContentJson" text NOT NULL,
    "ContentMarkdown" text,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean NOT NULL
);


--
-- Name: PackagePrice; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PackagePrice" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "PackageId" uuid NOT NULL,
    "Price" numeric(12,2) NOT NULL,
    "StartDate" timestamp with time zone,
    "EndDate" timestamp with time zone,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "DurationDays" integer DEFAULT 30 NOT NULL
);


--
-- Name: PaymentAttempts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PaymentAttempts" (
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
    "ExpiresAt" timestamp with time zone
);


--
-- Name: PaymentMethod; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PaymentMethod" (
    "Id" uuid CONSTRAINT "PaymentMethods_Id_not_null" NOT NULL,
    "MethodType" character varying(20) CONSTRAINT "PaymentMethods_MethodType_not_null" NOT NULL,
    "PaymentToken" character varying(500),
    "IsDefault" boolean CONSTRAINT "PaymentMethods_IsDefault_not_null" NOT NULL,
    "UserId" uuid CONSTRAINT "PaymentMethods_UserId_not_null" NOT NULL
);


--
-- Name: TABLE "PaymentMethod"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."PaymentMethod" IS 'Cấu hình phương thức thanh toán ưu tiên của người dùng';


--
-- Name: PaymentWebhookEvents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PaymentWebhookEvents" (
    "Id" uuid NOT NULL,
    "Provider" character varying(30) NOT NULL,
    "ProviderEventKey" character varying(255) NOT NULL,
    "OrderCode" bigint NOT NULL,
    "SignatureHash" character varying(64) NOT NULL,
    "PayloadHash" character varying(64) NOT NULL,
    "ProcessingStatus" character varying(20) NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    "ProcessedAt" timestamp with time zone,
    "Error" character varying(500)
);


--
-- Name: Payments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Payments" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "OrderId" uuid NOT NULL,
    "BoothOwnerId" uuid NOT NULL,
    "Type" character varying(20) NOT NULL,
    "Amount" numeric(12,2) NOT NULL,
    "Status" character varying(20) DEFAULT 'Pending'::character varying NOT NULL,
    "GatewayRef" character varying(255),
    "RefundReason" character varying(500),
    "PaidAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "CheckoutUrl" character varying(2000),
    "PaymentLinkId" character varying(255),
    "Gateway" character varying(20) NOT NULL,
    "PayOSOrderCode" bigint,
    "PayoutId" character varying(100),
    "RefundAmount" numeric(12,2),
    "RefundReference" character varying(100),
    "RefundRequestedAt" timestamp with time zone,
    "RefundedAt" timestamp with time zone,
    "PayoutCreateClaimedAt" timestamp with time zone,
    "CancelledAt" timestamp with time zone,
    "ExpiresAt" timestamp with time zone,
    "FailedAt" timestamp with time zone,
    "FailureCode" character varying(100),
    "FailureMessage" character varying(500),
    "QrCode" character varying(4000),
    "RowVersion" bytea DEFAULT uuid_send(gen_random_uuid()) NOT NULL
);


--
-- Name: TABLE "Payments"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Payments" IS 'Lịch sử giao dịch thanh toán/hoàn tiền - tích hợp đa cổng VNPay/ZaloPay/MoMo/Payos';


--
-- Name: COLUMN "Payments"."Type"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Payments"."Type" IS 'Tiền mặt hoặc PayOS';


--
-- Name: COLUMN "Payments"."GatewayRef"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Payments"."GatewayRef" IS 'Mã tra soát thực tế của ngân hàng (Ví dụ mã giao dịch của BIDV...)';


--
-- Name: COLUMN "Payments"."RefundReason"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Payments"."RefundReason" IS 'Lý do hoàn tiền (Nếu có)';


--
-- Name: COLUMN "Payments"."PaidAt"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Payments"."PaidAt" IS 'Thời điểm dòng tiền thực tế được khách hàng quét mã và bắn về hệ thống thành công';


--
-- Name: COLUMN "Payments"."CheckoutUrl"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Payments"."CheckoutUrl" IS 'Đường link thanh toán VietQR động ngắn hạn do PayOS trả về';


--
-- Name: COLUMN "Payments"."PaymentLinkId"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Payments"."PaymentLinkId" IS 'ID quản lý liên kết link thanh toán của hệ thống PayOS';


--
-- Name: COLUMN "Payments"."PayOSOrderCode"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Payments"."PayOSOrderCode" IS 'PayOS order code for this specific payment transaction (supplemental payments)';


--
-- Name: Promotion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Promotion" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "BoothId" uuid NOT NULL,
    "PromotionCode" character varying(50),
    "Title" character varying(200) NOT NULL,
    "Description" text,
    "DiscountType" character varying(20) NOT NULL,
    "DiscountValue" numeric(12,2) NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "Status" character varying(20) DEFAULT 'Scheduled'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "IsPublic" boolean DEFAULT false NOT NULL,
    "MaximumDiscountAmount" numeric(12,2),
    "MinimumOrderAmount" numeric(12,2),
    "Scope" character varying(30) DEFAULT 'EntireBoothOrder'::character varying NOT NULL,
    "TotalUsageLimit" integer,
    "UsageLimitPerCustomer" integer
);


--
-- Name: TABLE "Promotion"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Promotion" IS 'Chương trình khuyến mãi/mã giảm giá do gian hàng tạo';


--
-- Name: COLUMN "Promotion"."DiscountType"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Promotion"."DiscountType" IS 'Percentage: giảm % | FixedAmount: giảm số tiền cố định';


--
-- Name: PromotionCategory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PromotionCategory" (
    "PromotionId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL
);


--
-- Name: PromotionFoodItem; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PromotionFoodItem" (
    "PromotionId" uuid NOT NULL,
    "FoodItemId" uuid NOT NULL
);


--
-- Name: PromotionUsages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PromotionUsages" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "PromotionId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "AppliedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "DiscountAmount" numeric(12,2) DEFAULT 0.0 NOT NULL,
    "ReleasedAt" timestamp with time zone,
    "Status" character varying(20) DEFAULT 'Reserved'::character varying NOT NULL,
    "PromotionCodeSnapshot" character varying(50),
    "PromotionTitleSnapshot" character varying(200) NOT NULL
);


--
-- Name: TABLE "PromotionUsages"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."PromotionUsages" IS 'Lịch sử sử dụng mã khuyến mãi - kiểm tra UsageLimit và chống dùng trùng';


--
-- Name: ReviewReplies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ReviewReplies" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "ReviewId" uuid NOT NULL,
    "BoothOwnerId" uuid NOT NULL,
    "Content" text NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "ReviewReplies"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."ReviewReplies" IS 'Phản hồi của chủ gian hàng đối với đánh giá - quan hệ 1-1 với Reviews';


--
-- Name: Reviews; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Reviews" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "BoothId" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "Rating" smallint NOT NULL,
    "Content" text,
    "ImageUrl" character varying(500),
    "IsVisible" boolean DEFAULT true NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_reviews_rating CHECK ((("Rating" >= 1) AND ("Rating" <= 5)))
);


--
-- Name: TABLE "Reviews"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Reviews" IS 'Đánh giá của khách hàng cho gian hàng, gắn liền với 1 đơn hàng đã hoàn tất';


--
-- Name: COLUMN "Reviews"."IsVisible"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."Reviews"."IsVisible" IS 'false: Admin ẩn review nhưng vẫn giữ dữ liệu để tính rating';


--
-- Name: Role; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Role" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "RoleName" character varying(50) NOT NULL,
    "Description" text,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "Role"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."Role" IS 'Danh sách vai trò người dùng trong hệ thống (Customer, BoothOwner, Admin)';


--
-- Name: SupportAttachments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."SupportAttachments" (
    "Id" uuid NOT NULL,
    "TicketId" uuid NOT NULL,
    "MessageId" uuid,
    "FileUrl" character varying(1000) NOT NULL,
    "OriginalFileName" character varying(255) NOT NULL,
    "ContentType" character varying(100) NOT NULL,
    "FileSize" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: SupportMessages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."SupportMessages" (
    "Id" uuid NOT NULL,
    "TicketId" uuid NOT NULL,
    "SenderId" uuid NOT NULL,
    "SenderRole" character varying(30) NOT NULL,
    "Body" character varying(4000) NOT NULL,
    "IsInternalNote" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: SupportStatusHistories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."SupportStatusHistories" (
    "Id" uuid NOT NULL,
    "TicketId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "FromStatus" character varying(30),
    "ToStatus" character varying(30) NOT NULL,
    "Note" character varying(1000),
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: SupportTickets; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."SupportTickets" (
    "Id" uuid NOT NULL,
    "TicketCode" character varying(24) NOT NULL,
    "RequesterId" uuid NOT NULL,
    "RequesterRole" character varying(30) NOT NULL,
    "BoothId" uuid,
    "NightMarketId" uuid,
    "Category" character varying(50) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" character varying(4000) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "Priority" character varying(20) NOT NULL,
    "PageUrl" character varying(1000),
    "AssignedAdminId" uuid,
    "DueAt" timestamp with time zone NOT NULL,
    "FirstRespondedAt" timestamp with time zone,
    "ResolvedAt" timestamp with time zone,
    "ClosedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: SystemSetting; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."SystemSetting" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "Key" character varying(100) NOT NULL,
    "Value" text NOT NULL,
    "Description" character varying(500),
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: User; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."User" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "RoleId" uuid NOT NULL,
    "UserName" character varying(100) NOT NULL,
    "PasswordHash" character varying(255) NOT NULL,
    "FullName" character varying(150) NOT NULL,
    "Email" character varying(150) NOT NULL,
    "Phone" character varying(20),
    "Address" character varying(255),
    "DoB" date,
    "AvatarUrl" character varying(500),
    "AuthProvider" character varying(30) DEFAULT 'Local'::character varying NOT NULL,
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
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "User"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."User" IS 'Tài khoản người dùng - dùng chung cho Customer, BoothOwner, Admin (phân biệt qua RoleId)';


--
-- Name: COLUMN "User"."PasswordHash"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."User"."PasswordHash" IS 'Mật khẩu đã được mã hóa (hash), tuyệt đối không lưu plaintext';


--
-- Name: COLUMN "User"."Status"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public."User"."Status" IS 'Active: đang hoạt động | Inactive: chưa xác thực | Banned: bị khóa bởi Admin';


--
-- Name: UserDeviceToken; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UserDeviceToken" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "UserId" uuid NOT NULL,
    "Token" character varying(4096) NOT NULL,
    "Platform" character varying(20) NOT NULL,
    "DeviceId" character varying(200),
    "IsActive" boolean DEFAULT true NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "LastUsedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE "UserDeviceToken"; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public."UserDeviceToken" IS 'FCM device tokens registered by users';


--
-- Name: UserStatusHistories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UserStatusHistories" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ChangedByAdminId" uuid NOT NULL,
    "PreviousStatus" character varying(20) NOT NULL,
    "NewStatus" character varying(20) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: Zones; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Zones" (
    "Id" uuid DEFAULT public.uuid_generate_v4() NOT NULL,
    "NightMarketId" uuid NOT NULL,
    "ZoneName" character varying(100) NOT NULL,
    "Description" text,
    "Color" character varying(50),
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "IsDeleted" boolean DEFAULT false NOT NULL,
    "Capacity" integer DEFAULT 0 NOT NULL,
    "DefaultBoothHeight" double precision DEFAULT 60.0 NOT NULL,
    "DefaultBoothWidth" double precision DEFAULT 80.0 NOT NULL,
    "DefaultGap" double precision DEFAULT 20.0 NOT NULL,
    "ZoneCode" character varying(20),
    "WidthMeters" double precision,
    "LengthMeters" double precision,
    "BoothWidthMeters" double precision,
    "BoothLengthMeters" double precision,
    "HorizontalGapMeters" double precision,
    "VerticalGapMeters" double precision
);


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Name: payos_order_code_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.payos_order_code_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 99999999999999
    CACHE 1;


--
-- Name: AIRecommendationLog AIRecommendationLog_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIRecommendationLog"
    ADD CONSTRAINT "AIRecommendationLog_pkey" PRIMARY KEY ("Id");


--
-- Name: BoothDocuments BoothDocuments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothDocuments"
    ADD CONSTRAINT "BoothDocuments_pkey" PRIMARY KEY ("Id");


--
-- Name: BoothImages BoothImages_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothImages"
    ADD CONSTRAINT "BoothImages_pkey" PRIMARY KEY ("Id");


--
-- Name: BoothLocations BoothLocations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothLocations"
    ADD CONSTRAINT "BoothLocations_pkey" PRIMARY KEY ("Id");


--
-- Name: BoothPaymentInfos BoothPaymentInfos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothPaymentInfos"
    ADD CONSTRAINT "BoothPaymentInfos_pkey" PRIMARY KEY ("Id");


--
-- Name: BoothSubscriptions BoothSubscriptions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothSubscriptions"
    ADD CONSTRAINT "BoothSubscriptions_pkey" PRIMARY KEY ("Id");


--
-- Name: Booth Booth_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Booth"
    ADD CONSTRAINT "Booth_pkey" PRIMARY KEY ("Id");


--
-- Name: CartItem CartItem_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CartItem"
    ADD CONSTRAINT "CartItem_pkey" PRIMARY KEY ("Id");


--
-- Name: Cart Cart_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Cart"
    ADD CONSTRAINT "Cart_pkey" PRIMARY KEY ("Id");


--
-- Name: ComplaintImages ComplaintImages_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ComplaintImages"
    ADD CONSTRAINT "ComplaintImages_pkey" PRIMARY KEY ("Id");


--
-- Name: Complaints Complaints_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Complaints"
    ADD CONSTRAINT "Complaints_pkey" PRIMARY KEY ("Id");


--
-- Name: Conversations Conversations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "Conversations_pkey" PRIMARY KEY ("Id");


--
-- Name: CustomerPreference CustomerPreference_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CustomerPreference"
    ADD CONSTRAINT "CustomerPreference_pkey" PRIMARY KEY ("Id");


--
-- Name: FoodCategories FoodCategories_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodCategories"
    ADD CONSTRAINT "FoodCategories_pkey" PRIMARY KEY ("Id");


--
-- Name: FoodImages FoodImages_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodImages"
    ADD CONSTRAINT "FoodImages_pkey" PRIMARY KEY ("Id");


--
-- Name: FoodItemTag FoodItemTag_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodItemTag"
    ADD CONSTRAINT "FoodItemTag_pkey" PRIMARY KEY ("FoodItemId", "FoodTagId");


--
-- Name: FoodItem FoodItem_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodItem"
    ADD CONSTRAINT "FoodItem_pkey" PRIMARY KEY ("Id");


--
-- Name: FoodPrice FoodPrice_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodPrice"
    ADD CONSTRAINT "FoodPrice_pkey" PRIMARY KEY ("Id");


--
-- Name: FoodTag FoodTag_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodTag"
    ADD CONSTRAINT "FoodTag_pkey" PRIMARY KEY ("Id");


--
-- Name: LayoutBlocks LayoutBlocks_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutBlocks"
    ADD CONSTRAINT "LayoutBlocks_pkey" PRIMARY KEY ("Id");


--
-- Name: LayoutEdges LayoutEdges_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutEdges"
    ADD CONSTRAINT "LayoutEdges_pkey" PRIMARY KEY ("Id");


--
-- Name: LayoutNodes LayoutNodes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutNodes"
    ADD CONSTRAINT "LayoutNodes_pkey" PRIMARY KEY ("Id");


--
-- Name: MarketLayouts MarketLayouts_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MarketLayouts"
    ADD CONSTRAINT "MarketLayouts_pkey" PRIMARY KEY ("Id");


--
-- Name: MarketSubscriptions MarketSubscriptions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MarketSubscriptions"
    ADD CONSTRAINT "MarketSubscriptions_pkey" PRIMARY KEY ("Id");


--
-- Name: Message Message_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Message"
    ADD CONSTRAINT "Message_pkey" PRIMARY KEY ("Id");


--
-- Name: ModerationActionHistory ModerationActionHistory_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ModerationActionHistory"
    ADD CONSTRAINT "ModerationActionHistory_pkey" PRIMARY KEY ("Id");


--
-- Name: NightMarketImages NightMarketImages_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NightMarketImages"
    ADD CONSTRAINT "NightMarketImages_pkey" PRIMARY KEY ("Id");


--
-- Name: NightMarket NightMarket_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NightMarket"
    ADD CONSTRAINT "NightMarket_pkey" PRIMARY KEY ("Id");


--
-- Name: Notification Notification_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Notification"
    ADD CONSTRAINT "Notification_pkey" PRIMARY KEY ("Id");


--
-- Name: OrderDetail OrderDetail_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrderDetail"
    ADD CONSTRAINT "OrderDetail_pkey" PRIMARY KEY ("Id");


--
-- Name: Order Order_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Order"
    ADD CONSTRAINT "Order_pkey" PRIMARY KEY ("Id");


--
-- Name: BoothRegistrations PK_BoothRegistrations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothRegistrations"
    ADD CONSTRAINT "PK_BoothRegistrations" PRIMARY KEY ("Id");


--
-- Name: EmailOutbox PK_EmailOutbox; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."EmailOutbox"
    ADD CONSTRAINT "PK_EmailOutbox" PRIMARY KEY ("Id");


--
-- Name: PackagePolicies PK_PackagePolicies; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PackagePolicies"
    ADD CONSTRAINT "PK_PackagePolicies" PRIMARY KEY ("Id");


--
-- Name: PaymentAttempts PK_PaymentAttempts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PaymentAttempts"
    ADD CONSTRAINT "PK_PaymentAttempts" PRIMARY KEY ("Id");


--
-- Name: PaymentMethod PK_PaymentMethod; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PaymentMethod"
    ADD CONSTRAINT "PK_PaymentMethod" PRIMARY KEY ("Id");


--
-- Name: PaymentWebhookEvents PK_PaymentWebhookEvents; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PaymentWebhookEvents"
    ADD CONSTRAINT "PK_PaymentWebhookEvents" PRIMARY KEY ("Id");


--
-- Name: SupportAttachments PK_SupportAttachments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportAttachments"
    ADD CONSTRAINT "PK_SupportAttachments" PRIMARY KEY ("Id");


--
-- Name: SupportMessages PK_SupportMessages; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportMessages"
    ADD CONSTRAINT "PK_SupportMessages" PRIMARY KEY ("Id");


--
-- Name: SupportStatusHistories PK_SupportStatusHistories; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportStatusHistories"
    ADD CONSTRAINT "PK_SupportStatusHistories" PRIMARY KEY ("Id");


--
-- Name: SupportTickets PK_SupportTickets; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportTickets"
    ADD CONSTRAINT "PK_SupportTickets" PRIMARY KEY ("Id");


--
-- Name: SystemSetting PK_SystemSetting; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SystemSetting"
    ADD CONSTRAINT "PK_SystemSetting" PRIMARY KEY ("Id");


--
-- Name: UserStatusHistories PK_UserStatusHistories; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserStatusHistories"
    ADD CONSTRAINT "PK_UserStatusHistories" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: PackagePrice PackagePrice_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PackagePrice"
    ADD CONSTRAINT "PackagePrice_pkey" PRIMARY KEY ("Id");


--
-- Name: Package Package_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Package"
    ADD CONSTRAINT "Package_pkey" PRIMARY KEY ("Id");


--
-- Name: Payments Payments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Payments"
    ADD CONSTRAINT "Payments_pkey" PRIMARY KEY ("Id");


--
-- Name: PromotionCategory PromotionCategory_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionCategory"
    ADD CONSTRAINT "PromotionCategory_pkey" PRIMARY KEY ("PromotionId", "CategoryId");


--
-- Name: PromotionFoodItem PromotionFoodItem_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionFoodItem"
    ADD CONSTRAINT "PromotionFoodItem_pkey" PRIMARY KEY ("PromotionId", "FoodItemId");


--
-- Name: PromotionUsages PromotionUsages_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionUsages"
    ADD CONSTRAINT "PromotionUsages_pkey" PRIMARY KEY ("Id");


--
-- Name: Promotion Promotion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Promotion"
    ADD CONSTRAINT "Promotion_pkey" PRIMARY KEY ("Id");


--
-- Name: ReviewReplies ReviewReplies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ReviewReplies"
    ADD CONSTRAINT "ReviewReplies_pkey" PRIMARY KEY ("Id");


--
-- Name: Reviews Reviews_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Reviews"
    ADD CONSTRAINT "Reviews_pkey" PRIMARY KEY ("Id");


--
-- Name: Role Role_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Role"
    ADD CONSTRAINT "Role_pkey" PRIMARY KEY ("Id");


--
-- Name: UserDeviceToken UserDeviceToken_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserDeviceToken"
    ADD CONSTRAINT "UserDeviceToken_pkey" PRIMARY KEY ("Id");


--
-- Name: User User_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."User"
    ADD CONSTRAINT "User_pkey" PRIMARY KEY ("Id");


--
-- Name: Zones Zones_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Zones"
    ADD CONSTRAINT "Zones_pkey" PRIMARY KEY ("Id");


--
-- Name: FoodCategories_BoothId_Name_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "FoodCategories_BoothId_Name_key" ON public."FoodCategories" USING btree ("BoothId", "Name") WHERE ("IsDeleted" = false);


--
-- Name: IX_BoothDocuments_BoothId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothDocuments_BoothId" ON public."BoothDocuments" USING btree ("BoothId");


--
-- Name: IX_BoothDocuments_RegistrationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothDocuments_RegistrationId" ON public."BoothDocuments" USING btree ("RegistrationId");


--
-- Name: IX_BoothImages_BoothId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothImages_BoothId" ON public."BoothImages" USING btree ("BoothId");


--
-- Name: IX_BoothLocations_LayoutId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothLocations_LayoutId" ON public."BoothLocations" USING btree ("LayoutId");


--
-- Name: IX_BoothLocations_ZoneId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothLocations_ZoneId" ON public."BoothLocations" USING btree ("ZoneId");


--
-- Name: IX_BoothPaymentInfos_BoothId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothPaymentInfos_BoothId" ON public."BoothPaymentInfos" USING btree ("BoothId");


--
-- Name: IX_BoothRegistrations_BoothId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothRegistrations_BoothId" ON public."BoothRegistrations" USING btree ("BoothId");


--
-- Name: IX_BoothRegistrations_PreferredLayoutNodeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothRegistrations_PreferredLayoutNodeId" ON public."BoothRegistrations" USING btree ("PreferredLayoutNodeId");


--
-- Name: IX_BoothRegistrations_PreferredZoneId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothRegistrations_PreferredZoneId" ON public."BoothRegistrations" USING btree ("PreferredZoneId");


--
-- Name: IX_BoothRegistrations_RequestedNightMarketId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothRegistrations_RequestedNightMarketId" ON public."BoothRegistrations" USING btree ("RequestedNightMarketId");


--
-- Name: IX_BoothSubscriptions_BoothId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothSubscriptions_BoothId" ON public."BoothSubscriptions" USING btree ("BoothId");


--
-- Name: IX_BoothSubscriptions_BoothId_Active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_BoothSubscriptions_BoothId_Active" ON public."BoothSubscriptions" USING btree ("BoothId") WHERE (("Status")::text = 'Active'::text);


--
-- Name: IX_BoothSubscriptions_BoothId_PendingPayment; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_BoothSubscriptions_BoothId_PendingPayment" ON public."BoothSubscriptions" USING btree ("BoothId") WHERE (("Status")::text = 'PendingPayment'::text);


--
-- Name: IX_BoothSubscriptions_PackageId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BoothSubscriptions_PackageId" ON public."BoothSubscriptions" USING btree ("PackageId");


--
-- Name: IX_BoothSubscriptions_PayOSOrderCode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_BoothSubscriptions_PayOSOrderCode" ON public."BoothSubscriptions" USING btree ("PayOSOrderCode") WHERE ("PayOSOrderCode" IS NOT NULL);


--
-- Name: IX_Booth_RegistrationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Booth_RegistrationId" ON public."Booth" USING btree ("RegistrationId");


--
-- Name: IX_Booth_ZoneId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Booth_ZoneId" ON public."Booth" USING btree ("ZoneId");


--
-- Name: IX_ComplaintImages_ComplaintId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ComplaintImages_ComplaintId" ON public."ComplaintImages" USING btree ("ComplaintId");


--
-- Name: IX_Complaints_BoothId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Complaints_BoothId" ON public."Complaints" USING btree ("BoothId");


--
-- Name: IX_Complaints_OrderId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Complaints_OrderId" ON public."Complaints" USING btree ("OrderId");


--
-- Name: IX_Conversations_LastMessageId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Conversations_LastMessageId" ON public."Conversations" USING btree ("LastMessageId");


--
-- Name: IX_EmailOutbox_ReferenceId_EmailType; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_EmailOutbox_ReferenceId_EmailType" ON public."EmailOutbox" USING btree ("ReferenceId", "EmailType");


--
-- Name: IX_FoodImages_FoodItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_FoodImages_FoodItemId" ON public."FoodImages" USING btree ("FoodItemId");


--
-- Name: IX_FoodPrice_FoodItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_FoodPrice_FoodItemId" ON public."FoodPrice" USING btree ("FoodItemId");


--
-- Name: IX_LayoutBlocks_LayoutId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LayoutBlocks_LayoutId" ON public."LayoutBlocks" USING btree ("LayoutId");


--
-- Name: IX_LayoutBlocks_ZoneId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LayoutBlocks_ZoneId" ON public."LayoutBlocks" USING btree ("ZoneId");


--
-- Name: IX_LayoutEdges_FromNodeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LayoutEdges_FromNodeId" ON public."LayoutEdges" USING btree ("FromNodeId");


--
-- Name: IX_LayoutEdges_ToNodeId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LayoutEdges_ToNodeId" ON public."LayoutEdges" USING btree ("ToNodeId");


--
-- Name: IX_LayoutNodes_LayoutBlockId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LayoutNodes_LayoutBlockId" ON public."LayoutNodes" USING btree ("LayoutBlockId");


--
-- Name: IX_LayoutNodes_LayoutId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LayoutNodes_LayoutId" ON public."LayoutNodes" USING btree ("LayoutId");


--
-- Name: IX_LayoutNodes_ZoneId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LayoutNodes_ZoneId" ON public."LayoutNodes" USING btree ("ZoneId");


--
-- Name: IX_MarketSubscriptions_MarketOwnerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MarketSubscriptions_MarketOwnerId" ON public."MarketSubscriptions" USING btree ("MarketOwnerId");


--
-- Name: IX_MarketSubscriptions_MarketOwnerId_Active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MarketSubscriptions_MarketOwnerId_Active" ON public."MarketSubscriptions" USING btree ("MarketOwnerId") WHERE (("Status")::text = 'Active'::text);


--
-- Name: IX_MarketSubscriptions_MarketOwnerId_PendingPayment; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MarketSubscriptions_MarketOwnerId_PendingPayment" ON public."MarketSubscriptions" USING btree ("MarketOwnerId") WHERE (("Status")::text = 'PendingPayment'::text);


--
-- Name: IX_MarketSubscriptions_PackageId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MarketSubscriptions_PackageId" ON public."MarketSubscriptions" USING btree ("PackageId");


--
-- Name: IX_MarketSubscriptions_PayOSOrderCode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_MarketSubscriptions_PayOSOrderCode" ON public."MarketSubscriptions" USING btree ("PayOSOrderCode") WHERE ("PayOSOrderCode" IS NOT NULL);


--
-- Name: IX_NightMarket_MarketOwnerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_NightMarket_MarketOwnerId" ON public."NightMarket" USING btree ("MarketOwnerId");


--
-- Name: IX_Notification_BoothId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Notification_BoothId" ON public."Notification" USING btree ("BoothId");


--
-- Name: IX_OrderDetail_FoodItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OrderDetail_FoodItemId" ON public."OrderDetail" USING btree ("FoodItemId");


--
-- Name: IX_Order_BoothId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Order_BoothId" ON public."Order" USING btree ("BoothId");


--
-- Name: IX_Order_BoothOwnerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Order_BoothOwnerId" ON public."Order" USING btree ("BoothOwnerId");


--
-- Name: IX_PackagePolicies_PackageId_IsActive; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PackagePolicies_PackageId_IsActive" ON public."PackagePolicies" USING btree ("PackageId", "IsActive") WHERE (("IsActive" = true) AND ("IsDeleted" = false));


--
-- Name: IX_PackagePolicies_PackageId_Version; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PackagePolicies_PackageId_Version" ON public."PackagePolicies" USING btree ("PackageId", "Version");


--
-- Name: IX_PackagePrice_PackageId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PackagePrice_PackageId" ON public."PackagePrice" USING btree ("PackageId");


--
-- Name: IX_Package_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Package_Code" ON public."Package" USING btree ("Code") WHERE ("Code" IS NOT NULL);


--
-- Name: IX_PaymentAttempts_PaymentId_AttemptNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PaymentAttempts_PaymentId_AttemptNumber" ON public."PaymentAttempts" USING btree ("PaymentId", "AttemptNumber");


--
-- Name: IX_PaymentAttempts_ProviderOrderCode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PaymentAttempts_ProviderOrderCode" ON public."PaymentAttempts" USING btree ("ProviderOrderCode");


--
-- Name: IX_PaymentMethod_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PaymentMethod_UserId" ON public."PaymentMethod" USING btree ("UserId");


--
-- Name: IX_PaymentWebhookEvents_Provider_PayloadHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PaymentWebhookEvents_Provider_PayloadHash" ON public."PaymentWebhookEvents" USING btree ("Provider", "PayloadHash");


--
-- Name: IX_PaymentWebhookEvents_Provider_ProviderEventKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PaymentWebhookEvents_Provider_ProviderEventKey" ON public."PaymentWebhookEvents" USING btree ("Provider", "ProviderEventKey");


--
-- Name: IX_Payments_BoothOwnerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Payments_BoothOwnerId" ON public."Payments" USING btree ("BoothOwnerId");


--
-- Name: IX_PromotionUsages_CustomerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PromotionUsages_CustomerId" ON public."PromotionUsages" USING btree ("CustomerId");


--
-- Name: IX_PromotionUsages_OrderId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PromotionUsages_OrderId" ON public."PromotionUsages" USING btree ("OrderId");


--
-- Name: IX_ReviewReplies_BoothOwnerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ReviewReplies_BoothOwnerId" ON public."ReviewReplies" USING btree ("BoothOwnerId");


--
-- Name: IX_SupportAttachments_MessageId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_SupportAttachments_MessageId" ON public."SupportAttachments" USING btree ("MessageId");


--
-- Name: IX_SupportAttachments_TicketId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_SupportAttachments_TicketId" ON public."SupportAttachments" USING btree ("TicketId");


--
-- Name: IX_SupportMessages_TicketId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_SupportMessages_TicketId" ON public."SupportMessages" USING btree ("TicketId");


--
-- Name: IX_SupportStatusHistories_TicketId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_SupportStatusHistories_TicketId" ON public."SupportStatusHistories" USING btree ("TicketId");


--
-- Name: IX_SupportTickets_RequesterId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_SupportTickets_RequesterId_CreatedAt" ON public."SupportTickets" USING btree ("RequesterId", "CreatedAt");


--
-- Name: IX_SupportTickets_Status_DueAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_SupportTickets_Status_DueAt" ON public."SupportTickets" USING btree ("Status", "DueAt");


--
-- Name: IX_SupportTickets_TicketCode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_SupportTickets_TicketCode" ON public."SupportTickets" USING btree ("TicketCode");


--
-- Name: IX_SystemSetting_Key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_SystemSetting_Key" ON public."SystemSetting" USING btree ("Key");


--
-- Name: IX_UserStatusHistories_ChangedByAdminId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_UserStatusHistories_ChangedByAdminId" ON public."UserStatusHistories" USING btree ("ChangedByAdminId");


--
-- Name: IX_UserStatusHistories_UserId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_UserStatusHistories_UserId_CreatedAt" ON public."UserStatusHistories" USING btree ("UserId", "CreatedAt");


--
-- Name: IX_User_GoogleId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_User_GoogleId" ON public."User" USING btree ("GoogleId") WHERE ("GoogleId" IS NOT NULL);


--
-- Name: IX_User_RefreshTokenHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_User_RefreshTokenHash" ON public."User" USING btree ("RefreshTokenHash") WHERE ("RefreshTokenHash" IS NOT NULL);


--
-- Name: IX_User_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_User_RoleId" ON public."User" USING btree ("RoleId");


--
-- Name: Order_OrderCode_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "Order_OrderCode_key" ON public."Order" USING btree ("OrderCode");


--
-- Name: ReviewReplies_ReviewId_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "ReviewReplies_ReviewId_key" ON public."ReviewReplies" USING btree ("ReviewId");


--
-- Name: Role_RoleName_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "Role_RoleName_key" ON public."Role" USING btree ("RoleName");


--
-- Name: UserDeviceToken_Token_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UserDeviceToken_Token_key" ON public."UserDeviceToken" USING btree ("Token");


--
-- Name: User_Email_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "User_Email_key" ON public."User" USING btree ("Email");


--
-- Name: User_UserName_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "User_UserName_key" ON public."User" USING btree ("UserName");


--
-- Name: idx_airecommendationlog_customer; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_airecommendationlog_customer ON public."AIRecommendationLog" USING btree ("CustomerId");


--
-- Name: idx_airecommendationlog_nightmarket; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_airecommendationlog_nightmarket ON public."AIRecommendationLog" USING btree ("NightMarketId");


--
-- Name: idx_airecommendationlog_type_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_airecommendationlog_type_created ON public."AIRecommendationLog" USING btree ("RecommendationType", "CreatedAt");


--
-- Name: idx_booth_nightmarket; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_booth_nightmarket ON public."Booth" USING btree ("NightMarketId");


--
-- Name: idx_cartitem_cart; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_cartitem_cart ON public."CartItem" USING btree ("CartId");


--
-- Name: idx_cartitem_fooditem; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_cartitem_fooditem ON public."CartItem" USING btree ("FoodItemId");


--
-- Name: idx_complaint_customer_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_complaint_customer_created ON public."Complaints" USING btree ("CustomerId", "CreatedAt" DESC);


--
-- Name: idx_conversation_booth_last_message; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_conversation_booth_last_message ON public."Conversations" USING btree ("BoothId", "LastMessageAt");


--
-- Name: idx_conversation_customer_last_message; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_conversation_customer_last_message ON public."Conversations" USING btree ("CustomerId", "LastMessageAt");


--
-- Name: idx_conversation_last_message; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_conversation_last_message ON public."Conversations" USING btree ("LastMessageAt");


--
-- Name: idx_customerpreference_customer; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_customerpreference_customer ON public."CustomerPreference" USING btree ("CustomerId");


--
-- Name: idx_customerpreference_foodtag; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_customerpreference_foodtag ON public."CustomerPreference" USING btree ("FoodTagId");


--
-- Name: idx_device_token_user_active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_device_token_user_active ON public."UserDeviceToken" USING btree ("UserId", "IsActive");


--
-- Name: idx_foodcategory_booth; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_foodcategory_booth ON public."FoodCategories" USING btree ("BoothId");


--
-- Name: idx_fooditem_booth; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fooditem_booth ON public."FoodItem" USING btree ("BoothId");


--
-- Name: idx_fooditem_category; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fooditem_category ON public."FoodItem" USING btree ("CategoryId");


--
-- Name: idx_fooditemtag_foodtag; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fooditemtag_foodtag ON public."FoodItemTag" USING btree ("FoodTagId");


--
-- Name: idx_message_conversation; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_message_conversation ON public."Message" USING btree ("ConversationId", "CreatedAt", "Id");


--
-- Name: idx_moderationhistory_booth; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_moderationhistory_booth ON public."ModerationActionHistory" USING btree ("BoothId");


--
-- Name: idx_moderationhistory_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_moderationhistory_created ON public."ModerationActionHistory" USING btree ("CreatedAt");


--
-- Name: idx_moderationhistory_nightmarket; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_moderationhistory_nightmarket ON public."ModerationActionHistory" USING btree ("NightMarketId");


--
-- Name: idx_nightmarket_active_status_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_nightmarket_active_status_created ON public."NightMarket" USING btree ("IsDeleted", "Status", "CreatedAt");


--
-- Name: idx_nightmarket_moderation_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_nightmarket_moderation_status ON public."NightMarket" USING btree ("ModerationStatus");


--
-- Name: idx_nightmarketimage_market; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_nightmarketimage_market ON public."NightMarketImages" USING btree ("NightMarketId");


--
-- Name: idx_notification_batch_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_notification_batch_id ON public."Notification" USING btree ("BatchId");


--
-- Name: idx_notification_created_by_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_notification_created_by_created_at ON public."Notification" USING btree ("CreatedByUserId", "CreatedAt" DESC);


--
-- Name: idx_notification_user; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_notification_user ON public."Notification" USING btree ("UserId", "CreatedAt" DESC);


--
-- Name: idx_notification_user_read; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_notification_user_read ON public."Notification" USING btree ("UserId", "IsRead", "CreatedAt" DESC);


--
-- Name: idx_order_customer; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_order_customer ON public."Order" USING btree ("CustomerId");


--
-- Name: idx_order_customer_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_order_customer_created ON public."Order" USING btree ("CustomerId", "CreatedAt" DESC);


--
-- Name: idx_order_customer_status_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_order_customer_status_created ON public."Order" USING btree ("CustomerId", "Status", "CreatedAt" DESC);


--
-- Name: idx_orderdetail_order; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_orderdetail_order ON public."OrderDetail" USING btree ("OrderId");


--
-- Name: idx_payments_order; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_payments_order ON public."Payments" USING btree ("OrderId");


--
-- Name: idx_payments_payos_ordercode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_payments_payos_ordercode ON public."Payments" USING btree ("PayOSOrderCode") WHERE ("PayOSOrderCode" IS NOT NULL);


--
-- Name: idx_promotion_booth; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_promotion_booth ON public."Promotion" USING btree ("BoothId");


--
-- Name: idx_promotioncategory_category; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_promotioncategory_category ON public."PromotionCategory" USING btree ("CategoryId");


--
-- Name: idx_promotionfooditem_food; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_promotionfooditem_food ON public."PromotionFoodItem" USING btree ("FoodItemId");


--
-- Name: idx_reviews_booth; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_reviews_booth ON public."Reviews" USING btree ("BoothId");


--
-- Name: idx_reviews_booth_visible_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_reviews_booth_visible_created ON public."Reviews" USING btree ("BoothId", "IsVisible", "CreatedAt" DESC);


--
-- Name: idx_reviews_customer_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_reviews_customer_created ON public."Reviews" USING btree ("CustomerId", "CreatedAt" DESC);


--
-- Name: uq_booth_owner; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_booth_owner ON public."Booth" USING btree ("BoothOwnerId");


--
-- Name: uq_complaint_active_customer_order_booth; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_complaint_active_customer_order_booth ON public."Complaints" USING btree ("CustomerId", "OrderId", "BoothId") WHERE (("Status")::text = 'Pending'::text);


--
-- Name: uq_conversation_customer_booth; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_conversation_customer_booth ON public."Conversations" USING btree ("CustomerId", "BoothId");


--
-- Name: uq_pending_booth_registration_owner; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_pending_booth_registration_owner ON public."BoothRegistrations" USING btree ("OwnerId") WHERE ("Status" = 1);


--
-- Name: uq_promotionusage_order; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_promotionusage_order ON public."PromotionUsages" USING btree ("PromotionId", "OrderId");


--
-- Name: uq_review_order; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_review_order ON public."Reviews" USING btree ("OrderId");


--
-- Name: ux_boothlocation_active_booth; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_boothlocation_active_booth ON public."BoothLocations" USING btree ("BoothId") WHERE ("IsDeleted" = false);


--
-- Name: ux_boothlocation_active_node; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_boothlocation_active_node ON public."BoothLocations" USING btree ("LayoutNodeId") WHERE ("IsDeleted" = false);


--
-- Name: ux_cart_active_customer; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_cart_active_customer ON public."Cart" USING btree ("CustomerId") WHERE ("IsDeleted" = false);


--
-- Name: ux_cartitem_active_food; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_cartitem_active_food ON public."CartItem" USING btree ("CartId", "FoodItemId") WHERE ("IsDeleted" = false);


--
-- Name: ux_customerpreference_tag_kind; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_customerpreference_tag_kind ON public."CustomerPreference" USING btree ("CustomerId", "FoodTagId", "PreferenceKind");


--
-- Name: ux_foodcategory_code_active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_foodcategory_code_active ON public."FoodCategories" USING btree ("Code") WHERE ("IsDeleted" = false);


--
-- Name: ux_foodtag_code_active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_foodtag_code_active ON public."FoodTag" USING btree ("Code") WHERE ("IsDeleted" = false);


--
-- Name: ux_foodtag_name_active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_foodtag_name_active ON public."FoodTag" USING btree ("Name") WHERE ("IsDeleted" = false);


--
-- Name: ux_layoutedge_active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_layoutedge_active ON public."LayoutEdges" USING btree ("LayoutId", "FromNodeId", "ToNodeId") WHERE ("IsDeleted" = false);


--
-- Name: ux_layoutnodes_active_layout_slotcode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_layoutnodes_active_layout_slotcode ON public."LayoutNodes" USING btree ("LayoutId", "SlotCode") WHERE (("IsDeleted" = false) AND ("SlotCode" IS NOT NULL));


--
-- Name: ux_marketlayout_market_name_active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_marketlayout_market_name_active ON public."MarketLayouts" USING btree ("NightMarketId", "LayoutName") WHERE ("IsDeleted" = false);


--
-- Name: ux_marketlayout_market_version_active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_marketlayout_market_version_active ON public."MarketLayouts" USING btree ("NightMarketId", "Version") WHERE ("IsDeleted" = false);


--
-- Name: ux_marketlayout_one_active_per_market; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_marketlayout_one_active_per_market ON public."MarketLayouts" USING btree ("NightMarketId") WHERE (("IsDeleted" = false) AND (("Status")::text = 'Active'::text));


--
-- Name: ux_message_sender_client_message; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_message_sender_client_message ON public."Message" USING btree ("SenderId", "ClientMessageId") WHERE ("ClientMessageId" IS NOT NULL);


--
-- Name: ux_nightmarketimage_one_cover; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_nightmarketimage_one_cover ON public."NightMarketImages" USING btree ("NightMarketId") WHERE (("IsCover" = true) AND ("IsDeleted" = false));


--
-- Name: ux_order_customer_checkout_request; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_order_customer_checkout_request ON public."Order" USING btree ("CustomerId", "CheckoutRequestId") WHERE ("CheckoutRequestId" IS NOT NULL);


--
-- Name: ux_order_customer_idempotency_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_order_customer_idempotency_key ON public."Order" USING btree ("CustomerId", "IdempotencyKey") WHERE ("IdempotencyKey" IS NOT NULL);


--
-- Name: ux_payments_one_pending_payos_per_order; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_payments_one_pending_payos_per_order ON public."Payments" USING btree ("OrderId") WHERE ((("Status")::text = 'Pending'::text) AND (("Gateway")::text = 'Payos'::text));


--
-- Name: ux_payments_payout_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_payments_payout_id ON public."Payments" USING btree ("PayoutId") WHERE ("PayoutId" IS NOT NULL);


--
-- Name: ux_payments_refund_reference; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_payments_refund_reference ON public."Payments" USING btree ("RefundReference") WHERE ("RefundReference" IS NOT NULL);


--
-- Name: ux_promotion_active_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_promotion_active_code ON public."Promotion" USING btree ("BoothId", "PromotionCode") WHERE (("PromotionCode" IS NOT NULL) AND ("IsDeleted" = false));


--
-- Name: ux_zone_market_code_active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_zone_market_code_active ON public."Zones" USING btree ("NightMarketId", "ZoneCode") WHERE (("ZoneCode" IS NOT NULL) AND ("IsDeleted" = false));


--
-- Name: ux_zone_market_name_active; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_zone_market_name_active ON public."Zones" USING btree ("NightMarketId", "ZoneName") WHERE ("IsDeleted" = false);


--
-- Name: Order trg_order_row_version; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_order_row_version BEFORE UPDATE ON public."Order" FOR EACH ROW EXECUTE FUNCTION public.snm_set_order_payment_row_version();


--
-- Name: Payments trg_payments_row_version; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_payments_row_version BEFORE UPDATE ON public."Payments" FOR EACH ROW EXECUTE FUNCTION public.snm_set_order_payment_row_version();


--
-- Name: AIRecommendationLog AIRecommendationLog_CustomerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIRecommendationLog"
    ADD CONSTRAINT "AIRecommendationLog_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES public."User"("Id") ON DELETE SET NULL;


--
-- Name: AIRecommendationLog AIRecommendationLog_NightMarketId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIRecommendationLog"
    ADD CONSTRAINT "AIRecommendationLog_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES public."NightMarket"("Id") ON DELETE SET NULL;


--
-- Name: BoothDocuments BoothDocuments_RegistrationId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothDocuments"
    ADD CONSTRAINT "BoothDocuments_RegistrationId_fkey" FOREIGN KEY ("RegistrationId") REFERENCES public."BoothRegistrations"("Id") ON DELETE CASCADE;


--
-- Name: BoothImages BoothImages_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothImages"
    ADD CONSTRAINT "BoothImages_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE CASCADE;


--
-- Name: BoothLocations BoothLocations_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothLocations"
    ADD CONSTRAINT "BoothLocations_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE CASCADE;


--
-- Name: BoothLocations BoothLocations_LayoutId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothLocations"
    ADD CONSTRAINT "BoothLocations_LayoutId_fkey" FOREIGN KEY ("LayoutId") REFERENCES public."MarketLayouts"("Id") ON DELETE CASCADE;


--
-- Name: BoothLocations BoothLocations_LayoutNodeId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothLocations"
    ADD CONSTRAINT "BoothLocations_LayoutNodeId_fkey" FOREIGN KEY ("LayoutNodeId") REFERENCES public."LayoutNodes"("Id") ON DELETE RESTRICT;


--
-- Name: BoothLocations BoothLocations_ZoneId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothLocations"
    ADD CONSTRAINT "BoothLocations_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES public."Zones"("Id") ON DELETE SET NULL;


--
-- Name: BoothPaymentInfos BoothPaymentInfos_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothPaymentInfos"
    ADD CONSTRAINT "BoothPaymentInfos_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE CASCADE;


--
-- Name: BoothSubscriptions BoothSubscriptions_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothSubscriptions"
    ADD CONSTRAINT "BoothSubscriptions_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE CASCADE;


--
-- Name: BoothSubscriptions BoothSubscriptions_PackageId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothSubscriptions"
    ADD CONSTRAINT "BoothSubscriptions_PackageId_fkey" FOREIGN KEY ("PackageId") REFERENCES public."Package"("Id");


--
-- Name: Booth Booth_BoothOwnerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Booth"
    ADD CONSTRAINT "Booth_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES public."User"("Id");


--
-- Name: Booth Booth_NightMarketId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Booth"
    ADD CONSTRAINT "Booth_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES public."NightMarket"("Id") ON DELETE CASCADE;


--
-- Name: Booth Booth_RegistrationId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Booth"
    ADD CONSTRAINT "Booth_RegistrationId_fkey" FOREIGN KEY ("RegistrationId") REFERENCES public."BoothRegistrations"("Id");


--
-- Name: CartItem CartItem_CartId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CartItem"
    ADD CONSTRAINT "CartItem_CartId_fkey" FOREIGN KEY ("CartId") REFERENCES public."Cart"("Id");


--
-- Name: CartItem CartItem_FoodItemId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CartItem"
    ADD CONSTRAINT "CartItem_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES public."FoodItem"("Id");


--
-- Name: Cart Cart_CustomerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Cart"
    ADD CONSTRAINT "Cart_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES public."User"("Id");


--
-- Name: ComplaintImages ComplaintImages_ComplaintId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ComplaintImages"
    ADD CONSTRAINT "ComplaintImages_ComplaintId_fkey" FOREIGN KEY ("ComplaintId") REFERENCES public."Complaints"("Id") ON DELETE CASCADE;


--
-- Name: Complaints Complaints_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Complaints"
    ADD CONSTRAINT "Complaints_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id");


--
-- Name: Complaints Complaints_CustomerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Complaints"
    ADD CONSTRAINT "Complaints_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES public."User"("Id");


--
-- Name: Complaints Complaints_OrderId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Complaints"
    ADD CONSTRAINT "Complaints_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES public."Order"("Id");


--
-- Name: Conversations Conversations_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "Conversations_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id");


--
-- Name: Conversations Conversations_CustomerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "Conversations_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES public."User"("Id");


--
-- Name: Conversations Conversations_LastMessageId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Conversations"
    ADD CONSTRAINT "Conversations_LastMessageId_fkey" FOREIGN KEY ("LastMessageId") REFERENCES public."Message"("Id") ON DELETE SET NULL;


--
-- Name: CustomerPreference CustomerPreference_CustomerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CustomerPreference"
    ADD CONSTRAINT "CustomerPreference_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES public."User"("Id") ON DELETE CASCADE;


--
-- Name: CustomerPreference CustomerPreference_FoodTagId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CustomerPreference"
    ADD CONSTRAINT "CustomerPreference_FoodTagId_fkey" FOREIGN KEY ("FoodTagId") REFERENCES public."FoodTag"("Id") ON DELETE RESTRICT;


--
-- Name: BoothDocuments FK_BoothDocuments_Booth_BoothId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothDocuments"
    ADD CONSTRAINT "FK_BoothDocuments_Booth_BoothId" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE SET NULL;


--
-- Name: BoothRegistrations FK_BoothRegistrations_Booth_BoothId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothRegistrations"
    ADD CONSTRAINT "FK_BoothRegistrations_Booth_BoothId" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id");


--
-- Name: BoothRegistrations FK_BoothRegistrations_LayoutNodes_PreferredLayoutNodeId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothRegistrations"
    ADD CONSTRAINT "FK_BoothRegistrations_LayoutNodes_PreferredLayoutNodeId" FOREIGN KEY ("PreferredLayoutNodeId") REFERENCES public."LayoutNodes"("Id");


--
-- Name: BoothRegistrations FK_BoothRegistrations_NightMarket_RequestedNightMarketId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothRegistrations"
    ADD CONSTRAINT "FK_BoothRegistrations_NightMarket_RequestedNightMarketId" FOREIGN KEY ("RequestedNightMarketId") REFERENCES public."NightMarket"("Id") ON DELETE CASCADE;


--
-- Name: BoothRegistrations FK_BoothRegistrations_User_OwnerId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothRegistrations"
    ADD CONSTRAINT "FK_BoothRegistrations_User_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES public."User"("Id") ON DELETE CASCADE;


--
-- Name: BoothRegistrations FK_BoothRegistrations_Zones_PreferredZoneId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BoothRegistrations"
    ADD CONSTRAINT "FK_BoothRegistrations_Zones_PreferredZoneId" FOREIGN KEY ("PreferredZoneId") REFERENCES public."Zones"("Id");


--
-- Name: Booth FK_Booth_Zones_ZoneId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Booth"
    ADD CONSTRAINT "FK_Booth_Zones_ZoneId" FOREIGN KEY ("ZoneId") REFERENCES public."Zones"("Id");


--
-- Name: PackagePolicies FK_PackagePolicies_Package_PackageId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PackagePolicies"
    ADD CONSTRAINT "FK_PackagePolicies_Package_PackageId" FOREIGN KEY ("PackageId") REFERENCES public."Package"("Id") ON DELETE CASCADE;


--
-- Name: PaymentAttempts FK_PaymentAttempts_Payments_PaymentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PaymentAttempts"
    ADD CONSTRAINT "FK_PaymentAttempts_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES public."Payments"("Id") ON DELETE CASCADE;


--
-- Name: PaymentMethod FK_PaymentMethod_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PaymentMethod"
    ADD CONSTRAINT "FK_PaymentMethod_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("Id") ON DELETE CASCADE;


--
-- Name: SupportAttachments FK_SupportAttachments_SupportMessages_MessageId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportAttachments"
    ADD CONSTRAINT "FK_SupportAttachments_SupportMessages_MessageId" FOREIGN KEY ("MessageId") REFERENCES public."SupportMessages"("Id") ON DELETE SET NULL;


--
-- Name: SupportAttachments FK_SupportAttachments_SupportTickets_TicketId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportAttachments"
    ADD CONSTRAINT "FK_SupportAttachments_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES public."SupportTickets"("Id") ON DELETE CASCADE;


--
-- Name: SupportMessages FK_SupportMessages_SupportTickets_TicketId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportMessages"
    ADD CONSTRAINT "FK_SupportMessages_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES public."SupportTickets"("Id") ON DELETE CASCADE;


--
-- Name: SupportMessages FK_SupportMessages_User_SenderId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportMessages"
    ADD CONSTRAINT "FK_SupportMessages_User_SenderId" FOREIGN KEY ("SenderId") REFERENCES public."User"("Id") ON DELETE RESTRICT;


--
-- Name: SupportStatusHistories FK_SupportStatusHistories_SupportTickets_TicketId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportStatusHistories"
    ADD CONSTRAINT "FK_SupportStatusHistories_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES public."SupportTickets"("Id") ON DELETE CASCADE;


--
-- Name: SupportStatusHistories FK_SupportStatusHistories_User_ActorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportStatusHistories"
    ADD CONSTRAINT "FK_SupportStatusHistories_User_ActorId" FOREIGN KEY ("ActorId") REFERENCES public."User"("Id") ON DELETE RESTRICT;


--
-- Name: SupportTickets FK_SupportTickets_User_AssignedAdminId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportTickets"
    ADD CONSTRAINT "FK_SupportTickets_User_AssignedAdminId" FOREIGN KEY ("AssignedAdminId") REFERENCES public."User"("Id") ON DELETE RESTRICT;


--
-- Name: SupportTickets FK_SupportTickets_User_RequesterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SupportTickets"
    ADD CONSTRAINT "FK_SupportTickets_User_RequesterId" FOREIGN KEY ("RequesterId") REFERENCES public."User"("Id") ON DELETE RESTRICT;


--
-- Name: UserStatusHistories FK_UserStatusHistories_User_ChangedByAdminId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserStatusHistories"
    ADD CONSTRAINT "FK_UserStatusHistories_User_ChangedByAdminId" FOREIGN KEY ("ChangedByAdminId") REFERENCES public."User"("Id") ON DELETE RESTRICT;


--
-- Name: UserStatusHistories FK_UserStatusHistories_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserStatusHistories"
    ADD CONSTRAINT "FK_UserStatusHistories_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("Id") ON DELETE RESTRICT;


--
-- Name: FoodCategories FoodCategories_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodCategories"
    ADD CONSTRAINT "FoodCategories_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE CASCADE;


--
-- Name: FoodImages FoodImages_FoodItemId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodImages"
    ADD CONSTRAINT "FoodImages_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES public."FoodItem"("Id") ON DELETE CASCADE;


--
-- Name: FoodItemTag FoodItemTag_FoodItemId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodItemTag"
    ADD CONSTRAINT "FoodItemTag_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES public."FoodItem"("Id") ON DELETE CASCADE;


--
-- Name: FoodItemTag FoodItemTag_FoodTagId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodItemTag"
    ADD CONSTRAINT "FoodItemTag_FoodTagId_fkey" FOREIGN KEY ("FoodTagId") REFERENCES public."FoodTag"("Id") ON DELETE RESTRICT;


--
-- Name: FoodItem FoodItem_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodItem"
    ADD CONSTRAINT "FoodItem_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE CASCADE;


--
-- Name: FoodItem FoodItem_CategoryId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodItem"
    ADD CONSTRAINT "FoodItem_CategoryId_fkey" FOREIGN KEY ("CategoryId") REFERENCES public."FoodCategories"("Id");


--
-- Name: FoodPrice FoodPrice_FoodItemId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FoodPrice"
    ADD CONSTRAINT "FoodPrice_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES public."FoodItem"("Id") ON DELETE CASCADE;


--
-- Name: LayoutBlocks LayoutBlocks_LayoutId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutBlocks"
    ADD CONSTRAINT "LayoutBlocks_LayoutId_fkey" FOREIGN KEY ("LayoutId") REFERENCES public."MarketLayouts"("Id") ON DELETE CASCADE;


--
-- Name: LayoutBlocks LayoutBlocks_ZoneId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutBlocks"
    ADD CONSTRAINT "LayoutBlocks_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES public."Zones"("Id") ON DELETE SET NULL;


--
-- Name: LayoutEdges LayoutEdges_FromNodeId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutEdges"
    ADD CONSTRAINT "LayoutEdges_FromNodeId_fkey" FOREIGN KEY ("FromNodeId") REFERENCES public."LayoutNodes"("Id") ON DELETE RESTRICT;


--
-- Name: LayoutEdges LayoutEdges_LayoutId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutEdges"
    ADD CONSTRAINT "LayoutEdges_LayoutId_fkey" FOREIGN KEY ("LayoutId") REFERENCES public."MarketLayouts"("Id") ON DELETE CASCADE;


--
-- Name: LayoutEdges LayoutEdges_ToNodeId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutEdges"
    ADD CONSTRAINT "LayoutEdges_ToNodeId_fkey" FOREIGN KEY ("ToNodeId") REFERENCES public."LayoutNodes"("Id") ON DELETE RESTRICT;


--
-- Name: LayoutNodes LayoutNodes_LayoutBlockId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutNodes"
    ADD CONSTRAINT "LayoutNodes_LayoutBlockId_fkey" FOREIGN KEY ("LayoutBlockId") REFERENCES public."LayoutBlocks"("Id") ON DELETE SET NULL;


--
-- Name: LayoutNodes LayoutNodes_LayoutId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutNodes"
    ADD CONSTRAINT "LayoutNodes_LayoutId_fkey" FOREIGN KEY ("LayoutId") REFERENCES public."MarketLayouts"("Id") ON DELETE CASCADE;


--
-- Name: LayoutNodes LayoutNodes_ZoneId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LayoutNodes"
    ADD CONSTRAINT "LayoutNodes_ZoneId_fkey" FOREIGN KEY ("ZoneId") REFERENCES public."Zones"("Id") ON DELETE SET NULL;


--
-- Name: MarketLayouts MarketLayouts_NightMarketId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MarketLayouts"
    ADD CONSTRAINT "MarketLayouts_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES public."NightMarket"("Id") ON DELETE CASCADE;


--
-- Name: MarketSubscriptions MarketSubscriptions_MarketOwnerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MarketSubscriptions"
    ADD CONSTRAINT "MarketSubscriptions_MarketOwnerId_fkey" FOREIGN KEY ("MarketOwnerId") REFERENCES public."User"("Id") ON DELETE CASCADE;


--
-- Name: MarketSubscriptions MarketSubscriptions_PackageId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MarketSubscriptions"
    ADD CONSTRAINT "MarketSubscriptions_PackageId_fkey" FOREIGN KEY ("PackageId") REFERENCES public."Package"("Id");


--
-- Name: Message Message_ConversationId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Message"
    ADD CONSTRAINT "Message_ConversationId_fkey" FOREIGN KEY ("ConversationId") REFERENCES public."Conversations"("Id") ON DELETE CASCADE;


--
-- Name: Message Message_SenderId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Message"
    ADD CONSTRAINT "Message_SenderId_fkey" FOREIGN KEY ("SenderId") REFERENCES public."User"("Id");


--
-- Name: ModerationActionHistory ModerationActionHistory_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ModerationActionHistory"
    ADD CONSTRAINT "ModerationActionHistory_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE SET NULL;


--
-- Name: ModerationActionHistory ModerationActionHistory_NightMarketId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ModerationActionHistory"
    ADD CONSTRAINT "ModerationActionHistory_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES public."NightMarket"("Id") ON DELETE SET NULL;


--
-- Name: NightMarketImages NightMarketImages_NightMarketId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NightMarketImages"
    ADD CONSTRAINT "NightMarketImages_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES public."NightMarket"("Id") ON DELETE CASCADE;


--
-- Name: NightMarket NightMarket_MarketOwnerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NightMarket"
    ADD CONSTRAINT "NightMarket_MarketOwnerId_fkey" FOREIGN KEY ("MarketOwnerId") REFERENCES public."User"("Id") ON DELETE SET NULL;


--
-- Name: Notification Notification_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Notification"
    ADD CONSTRAINT "Notification_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE SET NULL;


--
-- Name: Notification Notification_UserId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Notification"
    ADD CONSTRAINT "Notification_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES public."User"("Id") ON DELETE CASCADE;


--
-- Name: OrderDetail OrderDetail_FoodItemId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrderDetail"
    ADD CONSTRAINT "OrderDetail_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES public."FoodItem"("Id");


--
-- Name: OrderDetail OrderDetail_OrderId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OrderDetail"
    ADD CONSTRAINT "OrderDetail_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES public."Order"("Id") ON DELETE CASCADE;


--
-- Name: Order Order_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Order"
    ADD CONSTRAINT "Order_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE RESTRICT;


--
-- Name: Order Order_BoothOwnerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Order"
    ADD CONSTRAINT "Order_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES public."User"("Id") ON DELETE RESTRICT;


--
-- Name: Order Order_CustomerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Order"
    ADD CONSTRAINT "Order_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES public."User"("Id");


--
-- Name: PackagePrice PackagePrice_PackageId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PackagePrice"
    ADD CONSTRAINT "PackagePrice_PackageId_fkey" FOREIGN KEY ("PackageId") REFERENCES public."Package"("Id") ON DELETE CASCADE;


--
-- Name: Payments Payments_BoothOwnerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Payments"
    ADD CONSTRAINT "Payments_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES public."User"("Id");


--
-- Name: Payments Payments_OrderId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Payments"
    ADD CONSTRAINT "Payments_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES public."Order"("Id") ON DELETE CASCADE;


--
-- Name: PromotionCategory PromotionCategory_CategoryId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionCategory"
    ADD CONSTRAINT "PromotionCategory_CategoryId_fkey" FOREIGN KEY ("CategoryId") REFERENCES public."FoodCategories"("Id") ON DELETE RESTRICT;


--
-- Name: PromotionCategory PromotionCategory_PromotionId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionCategory"
    ADD CONSTRAINT "PromotionCategory_PromotionId_fkey" FOREIGN KEY ("PromotionId") REFERENCES public."Promotion"("Id") ON DELETE CASCADE;


--
-- Name: PromotionFoodItem PromotionFoodItem_FoodItemId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionFoodItem"
    ADD CONSTRAINT "PromotionFoodItem_FoodItemId_fkey" FOREIGN KEY ("FoodItemId") REFERENCES public."FoodItem"("Id") ON DELETE RESTRICT;


--
-- Name: PromotionFoodItem PromotionFoodItem_PromotionId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionFoodItem"
    ADD CONSTRAINT "PromotionFoodItem_PromotionId_fkey" FOREIGN KEY ("PromotionId") REFERENCES public."Promotion"("Id") ON DELETE CASCADE;


--
-- Name: PromotionUsages PromotionUsages_CustomerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionUsages"
    ADD CONSTRAINT "PromotionUsages_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES public."User"("Id");


--
-- Name: PromotionUsages PromotionUsages_OrderId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionUsages"
    ADD CONSTRAINT "PromotionUsages_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES public."Order"("Id") ON DELETE CASCADE;


--
-- Name: PromotionUsages PromotionUsages_PromotionId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PromotionUsages"
    ADD CONSTRAINT "PromotionUsages_PromotionId_fkey" FOREIGN KEY ("PromotionId") REFERENCES public."Promotion"("Id") ON DELETE CASCADE;


--
-- Name: Promotion Promotion_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Promotion"
    ADD CONSTRAINT "Promotion_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE CASCADE;


--
-- Name: ReviewReplies ReviewReplies_BoothOwnerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ReviewReplies"
    ADD CONSTRAINT "ReviewReplies_BoothOwnerId_fkey" FOREIGN KEY ("BoothOwnerId") REFERENCES public."User"("Id");


--
-- Name: ReviewReplies ReviewReplies_ReviewId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ReviewReplies"
    ADD CONSTRAINT "ReviewReplies_ReviewId_fkey" FOREIGN KEY ("ReviewId") REFERENCES public."Reviews"("Id") ON DELETE CASCADE;


--
-- Name: Reviews Reviews_BoothId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Reviews"
    ADD CONSTRAINT "Reviews_BoothId_fkey" FOREIGN KEY ("BoothId") REFERENCES public."Booth"("Id") ON DELETE CASCADE;


--
-- Name: Reviews Reviews_CustomerId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Reviews"
    ADD CONSTRAINT "Reviews_CustomerId_fkey" FOREIGN KEY ("CustomerId") REFERENCES public."User"("Id");


--
-- Name: Reviews Reviews_OrderId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Reviews"
    ADD CONSTRAINT "Reviews_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES public."Order"("Id");


--
-- Name: UserDeviceToken UserDeviceToken_UserId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserDeviceToken"
    ADD CONSTRAINT "UserDeviceToken_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES public."User"("Id") ON DELETE CASCADE;


--
-- Name: User User_RoleId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."User"
    ADD CONSTRAINT "User_RoleId_fkey" FOREIGN KEY ("RoleId") REFERENCES public."Role"("Id");


--
-- Name: Zones Zones_NightMarketId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Zones"
    ADD CONSTRAINT "Zones_NightMarketId_fkey" FOREIGN KEY ("NightMarketId") REFERENCES public."NightMarket"("Id") ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict gfGdITz0wZW72fpsWOUgA8HjSKCwxQn4qgi89N7veIRA1TMVbIvYNmkrtkoMUMD

