using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities
{
    public class PaymentMethod
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public PaymentType MethodType { get; set; }

        // Lưu các thông tin mã định danh kết nối (ví dụ: Token từ cổng thanh toán trả về)
        // Tuyệt đối KHÔNG lưu số thẻ hay mật khẩu của khách để tránh vi phạm bảo mật
        public string PaymentToken { get; set; }

        public bool IsDefault { get; set; } = false; // Có phải là mặc định không

        // Khóa ngoại liên kết tới bảng User
        public virtual Guid UserId { get; set; }
        public virtual User User { get; set; } // Navigation property
    }
}
