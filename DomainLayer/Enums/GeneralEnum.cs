using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.Enums
{
    public class GeneralEnum
    {
        public enum BoothStatus
        {
            Pending = 0,
            Active = 1,
            Inactive = 2,
            Suspended = 3
        }

        public enum BoothDocumentStatus
        {
            Pending = 0,
            Verified = 1,
            Rejected = 2
        }

        public enum BoothPaymentType
        {
            BankTransfer = 0,
            VNPay = 1,
            MoMo = 2,
            ZaloPay = 3,
            Payos = 4
        }

        public enum BoothRegistrationStatus
        {
            Draft = 0,
            Pending = 1,
            Approved = 2,
            Rejected = 3,
            Cancelled = 4
        }

        public enum BoothSubscriptionStatus
        {
            Active = 0,
            Expired = 1,
            Cannelled = 2
        }

        public enum ComplaintStatus
        {
            Pending = 0,
            Resolved = 1,
            Rejected = 2
        }

        public enum MessageType
        {
            Text = 0,
            Image = 1,
            Video = 2,
            File = 3
        }

        public enum NightMarketStatus
        {
            Pending = 0,
            Active = 1,
            Inactive = 2,
            Suspended = 3
        }

        public enum NotificationType
        {
            NewOrder = 0,
            OrderStatusChanged = 1,
            NewMessage = 2,
            Promotion = 3, 
            System = 4
        }

        public enum OrderStatus
        {
            Pending = 0,
            Accepted = 1,
            Preparing = 2,
            ReadyForPickup = 3,
            Completed = 4,
            Cancelled = 5
        }

        public enum PaymentType
        {
            Payment = 0,
            Refund = 1
        }

        public enum PaymentMethod
        {
            BankTransfer = 0,
            VNPay = 1,
            MoMo = 2,
            ZaloPay = 3,
            Payos = 4
        }

        public enum PaymentStatus
        {
            Pending = 0,
            Paid = 1,
            Failed = 2
        }

        public enum DiscountType
        {
            Percentage = 0,
            FixedAmount = 1
        }

        public enum PromotionStatus
        {
            Active = 0,
            Expired = 1,
            Inactive = 2
        }
        
        public enum PromotionalPackageStatus
        {
            Active = 0,
            Expired = 1,
            Inactive = 2
        }

        public enum SubscriptionPackageStatus
        {
            Active = 0,
            Expired = 1,
            Inactive = 2
        }

        public enum UserStatus
        {
            Active = 0,
            Inactive = 1,
            Suspended = 2
        }
    }
}
