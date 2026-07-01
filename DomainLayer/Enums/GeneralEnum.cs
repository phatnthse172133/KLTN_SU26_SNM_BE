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
            Draft = 0,              // chưa submit
            PendingReview = 1,      // chờ duyệt
            Approved = 2,           // duyệt
            Rejected = 3,           // từ chối
            Cancelled = 4           // owner hủy đơn
        }

        public enum BoothDocumentType
        {
            BusinessLicense = 0,          // Giấy phép kinh doanh

            FoodSafetyCertificate = 1,    // Chứng nhận VSATTP

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
            PendingApproval = 0, // chờ duyệt
            Active = 1,          // hoạt động
            Inactive = 2,        // owner tạm đóng
            Suspended = 3,       // admin khóa
            Closed = 4           // ngừng kinh doanh
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
            Junction = 0,    // Điểm giao nhau hoặc điểm trung chuyển giữa các lối đi
            Entrance = 1,    // Cổng vào của chợ, có thể dùng làm điểm bắt đầu tìm đường
            Exit = 2,        // Cổng ra của chợ, có thể dùng làm điểm bắt đầu hoặc điểm đích
            BoothAccess = 3, // Điểm tiếp cận một gian hàng, dùng để gán BoothLocation và tìm đường đến Booth
            Landmark = 4     // Địa điểm nổi bật/dễ nhận biết như sân khấu, nhà vệ sinh hoặc khu check-in
        }

        public enum NightMarketStatus
        {
            Draft = 0,
            Upcoming = 1,
            Open = 2,
            Closed = 3,
            Cancelled = 4
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
            Submitted = 0,
            UnderInvestigation = 1,
            Resolved = 2,
            Rejected = 3,
            Closed = 4
        }

        public enum ComplaintResolutionAction
        {
            NoViolation = 0,
            Warning = 1,
            SuspendBooth = 2,
            CloseBooth = 3
        }

        public enum ConversationStatus
        {
            Active = 0,
            Closed = 1
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
            System = 6
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

        public enum PaymentType
        {
            BankTransfer = 0,
            Payos = 1,
            VNPay = 2,
            MoMo = 3
        }

        public enum PaymentStatus
        {
            Pending = 0,
            Paid = 1,
            Failed = 2,
            Cancelled = 3,
            Refunded = 4
        }

        public enum OrderStatus
        {
            Pending = 0,      // Chờ thanh toán / xác nhận
            Confirmed = 1,    // Đã thanh toán, quầy nhận đơn
            Ready = 2,        // Món đã xong, chờ khách lấy
            Completed = 3,    // Khách đã nhận món
            Cancelled = 4,    // Đơn bị hủy
            Expired = 5       // Quá thời gian nhận món
        }

        public enum PayOrderStatus
        {
            Pending = 0,
            Paid = 1,
            RefundPending = 2,
            Refunded = 3,
            Failed = 4
        }
    }
}
