using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.Enums
{
    public class GeneralEnum
    {
        public enum UserStatus
        {
            PendingVerification = 0, // vừa đăng ký
            Active = 1,              // hoạt động
            Suspended = 2,           // khóa tạm thời
            Banned = 3,              // khóa vĩnh viễn
            Inactive = 4             // tự ngưng sử dụng
        }

        public enum BoothRegistrationStatus
        {
            Draft = 0,              // chÆ°a submit
            PendingReview = 1,      // chá» duyá»‡t
            Approved = 2,           // duyá»‡t
            Rejected = 3,           // tá»« chá»‘i
            Cancelled = 4           // owner há»§y Ä‘Æ¡n
        }

        public enum BoothDocumentType
        {
            BusinessLicense = 0,          // Giáº¥y phÃ©p kinh doanh

            FoodSafetyCertificate = 1,    // Chá»©ng nháº­n VSATTP

            OwnerIdentification = 2,      // CCCD/CMND

            Other = 3
        }

        public enum BoothDocumentStatus
        {
            PendingReview = 0,
            Verified = 1,
            Rejected = 2
        }

        public enum BoothStatus
        {
            PendingApproval = 0, // chá» duyá»‡t
            Active = 1,          // hoáº¡t Ä‘á»™ng
            Inactive = 2,        // owner táº¡m Ä‘Ã³ng
            Suspended = 3,       // admin khÃ³a
            Closed = 4           // ngá»«ng kinh doanh
        }

        public enum ZoneStatus
        {
            Active = 0,
            Inactive = 1,
            Maintenance = 2
        }

        public enum MarketLayoutStatus
        {
            Draft = 0,
            Active = 1,
            Inactive = 2,
            Archived = 3
        }

        public enum LayoutNodeType
        {
            Junction = 0,    // Äiá»ƒm giao nhau hoáº·c Ä‘iá»ƒm trung chuyá»ƒn giá»¯a cÃ¡c lá»‘i Ä‘i
            Entrance = 1,    // Cá»•ng vÃ o cá»§a chá»£, cÃ³ thá»ƒ dÃ¹ng lÃ m Ä‘iá»ƒm báº¯t Ä‘áº§u tÃ¬m Ä‘Æ°á»ng
            Exit = 2,        // Cá»•ng ra cá»§a chá»£, cÃ³ thá»ƒ dÃ¹ng lÃ m Ä‘iá»ƒm báº¯t Ä‘áº§u hoáº·c Ä‘iá»ƒm Ä‘Ã­ch
            BoothAccess = 3, // Äiá»ƒm tiáº¿p cáº­n má»™t gian hÃ ng, dÃ¹ng Ä‘á»ƒ gÃ¡n BoothLocation vÃ  tÃ¬m Ä‘Æ°á»ng Ä‘áº¿n Booth
            Landmark = 4     // Äá»‹a Ä‘iá»ƒm ná»•i báº­t/dá»… nháº­n biáº¿t nhÆ° sÃ¢n kháº¥u, nhÃ  vá»‡ sinh hoáº·c khu check-in
        }

        public enum NightMarketStatus
        {
            Draft = 0,
            Upcoming = 1,
            Open = 2,
            Closed = 3,
            Cancelled = 4
        }

        public enum ModerationStatus
        {
            Active = 0,
            Suspended = 1
        }

        public enum ModerationActionSource
        {
            DirectAdmin = 0,
            Complaint = 1
        }

        public enum SubscriptionStatus
        {
            PendingPayment = 0,
            Active = 1,
            Expired = 2,
            Cancelled = 3
        }

        public enum PackageStatus
        {
            Active = 0,
            Inactive = 1
        }

        public enum PackageType
        {
            Booth = 0,
            Market = 1
        }

        public enum PromotionStatus
        {
            Scheduled = 0,
            Active = 1,
            Inactive = 2,
            Expired = 3,
            Suspended = 4
        }

        public enum DiscountType
        {
            Percentage = 0,
            FixedAmount = 1
        }

        public enum PromotionScope
        {
            EntireBoothOrder = 0,
            SpecificFoodItems = 1,
            SpecificCategories = 2
        }

        public enum PromotionUsageStatus
        {
            Reserved = 0,
            Consumed = 1,
            Released = 2
        }

        public enum ComplaintStatus
        {
            Pending = 0,
            Resolved = 1,
            Rejected = 2
        }

        public enum ComplaintResolutionAction
        {
            NoViolation = 0,
            Warning = 1,
            SuspendBooth = 2,
            CloseBooth = 3
        }

        public enum AuthProvider
        {
            Local = 0,
            Google = 1,
            LocalGoogle = 2
        }

        public enum ConversationStatus
        {
            Active = 0,
            Closed = 1
        }

        public enum ConversationParticipantRole
        {
            Customer = 0,
            BoothOwner = 1,
            MarketOwner = 2,
            Admin = 3,
            System = 4
        }

        public enum MessageType
        {
            Text = 0,
            Image = 1,
            System = 2,
            File = 3
        }

        public enum NotificationType
        {
            Order = 0,
            Payment = 1,
            Promotion = 2,
            Complaint = 3,
            Registration = 4,
            Subscription = 5,
            System = 6,
            NewMessage = 7,
            OrderCreated = 8,
            PaymentProofUploaded = 9,
            PaymentApproved = 10,
            PaymentRejected = 11,
            OrderPreparing = 12,
            OrderReady = 13,
            OrderCompleted = 14,
            OrderCancelled = 15,
            RefundPending = 16,
            RefundCompleted = 17,
            RegistrationSubmitted = 18,
            RegistrationApproved = 19,
            RegistrationRejected = 20,
            NewReview = 21,
            ReviewReplied = 22,
            ComplaintSubmitted = 23,
            ComplaintInReview = 24,
            ComplaintResolved = 25,
            ComplaintRejected = 26,
            SubscriptionActivated = 27,
            SubscriptionRejected = 28,
            SubscriptionExpired = 29,
            PaymentSucceeded = 30,
            PaymentFailed = 31,
            SystemAnnouncement = 32,
            AccountSuspended = 33,
            BoothSuspended = 34,
            AccountDeactivated = 35,
            AccountReactivated = 36
        }

        public enum DevicePlatform
        {
            Android = 0,
            IOS = 1,
            Web = 2
        }

        public enum NotificationTarget
        {
            AllUsers = 0,
            Role = 1,
            SpecificUser = 2
        }

        public enum BoothPaymentType
        {
            BankTransfer = 0,
            QRCode = 1
        }

        public enum BoothPaymentStatus
        {
            Active = 0,
            Inactive = 1
        }

        //public enum PaymentType
        //{
        //    BankTransfer = 0,
        //    Payos = 1,
        //    VNPay = 2,
        //    MoMo = 3
        //}

        //public enum OrderStatus
        //{
        //    Pending = 0,      // Chá» thanh toÃ¡n / xÃ¡c nháº­n
        //    Confirmed = 1,    // ÄÃ£ thanh toÃ¡n, quáº§y nháº­n Ä‘Æ¡n
        //    Ready = 2,        // MÃ³n Ä‘Ã£ xong, chá» khÃ¡ch láº¥y
        //    Completed = 3,    // KhÃ¡ch Ä‘Ã£ nháº­n mÃ³n
        //    Cancelled = 4,    // ÄÆ¡n bá»‹ há»§y
        //    Expired = 5       // QuÃ¡ thá»i gian nháº­n mÃ³n
        //}

        //public enum OrderStatus
        //{
        //    PendingPayment = 0,      // Chá» thanh toÃ¡n / xÃ¡c nháº­n
        //    Placed = 1,    // ÄÃ£ thanh toÃ¡n, quáº§y nháº­n Ä‘Æ¡n
        //    Preparing = 2,        // Äang chuáº©n bá»‹ mÃ³n
        //    ReadyForPickup = 3,    // MÃ³n Ä‘Ã£ xong, chá» khÃ¡ch láº¥y
        //    Completed = 4,      //HoÃ n thÃ nh
        //    Cancelled = 5,    // ÄÆ¡n bá»‹ há»§y
        //    Refunded = 6       // ÄÆ¡n bá»‹ tá»« chá»‘i
        //}

        public enum PaymentType
        {
            Cash = 0,       // Tiá»n máº·t
            PayOS = 1  // TÃ­ch há»£p cá»•ng thanh toÃ¡n PayOS
        }

        public enum PaymentGateway
        {
            BankTransfer = 0,
            Payos = 1,
            VNPay = 2,
            MoMo = 3,
            ZaloPay = 4
        }

        public enum PaymentStatus
        {
            Pending = 0, // Chưa thanh toán (Mặc định khi tạo đơn PayOS)
            Paid = 1, // Đã thu tiền thành công (Cập nhật khi Webhook Ting Ting)
            Failed = 2, // Thanh toán thất bại, chủ quán không nhận được tiền
            Refunded = 3, // Đã hoàn tiền cho khách
            Cancelled = 4,
            RefundProcessing = 5, // Đang xử lý hoàn tiền
            Underpaid = 6 // Thanh toán thiếu (Phát hiện gian lận hoặc lỗi dòng tiền)
        }

        public enum OrderStatus
        {
            Placed = 0,          // Đơn hàng mới đã được hệ thống ghi nhận
            Preparing = 1,       // Quầy đang chuẩn bị món
            ReadyForPickup = 2,  // Món đã xong, chờ khách đến lấy (hoàn thành món nhưng chưa trả tiền cho quầy nếu customer chọn
                                 // Cash)
            Completed = 3,       // Khách đã lấy món -> Hoàn thành đơn (đã trả tiền cho quầy nếu customer chọn Cash)
            Cancelled = 4,        // Đơn bị hủy (Do khách hủy hoặc quầy hết nguyên liệu)
            Underpaid = 5,        // Thanh toán thiếu
            Refunded = 6          // Đơn đã được hoàn tiền cho khách
        }

        //public enum PayOrderStatus
        //{
        //    Pending = 0,
        //    Paid = 1,
        //    RefundPending = 2,
        //    Refunded = 3,
        //    Failed = 4
        //}

        public enum FoodTagGroup
        {
            Taste = 0,
            Temperature = 1,
            MealPurpose = 2,
            CookingMethod = 3,
            Ingredient = 4,
            Dietary = 5,
            Budget = 6,
            Other = 7
        }

        public enum FoodTagStatus
        {
            Active = 0,
            Inactive = 1
        }

        public enum CustomerPreferenceKind
        {
            Like = 0,
            Avoid = 1
        }

        public enum CustomerPreferenceSource
        {
            UserSelected = 0,
            AIInferred = 1,
            PurchaseHistory = 2,
            ReviewHistory = 3
        }

        public enum AIRecommendationType
        {
            FoodDiscovery = 0,
            DiningPlan = 1,
            PreferenceProfile = 2
        }
    }
}
