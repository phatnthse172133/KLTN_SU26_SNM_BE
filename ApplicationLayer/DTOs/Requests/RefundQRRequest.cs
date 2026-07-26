using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Requests
{
    //Dùng để chứa các thông tin cần thiết để thực hiện yêu cầu hoàn tiền (refund) thông qua mã QR customer gửi lên. Các thông tin này có thể bao gồm lý do hoàn tiền, thông tin ngân hàng và số tài khoản của khách hàng.
    public class RefundQRRequest
    {
        public string? RefundReason { get; set; }
        public string? BankBin { get; set; }
        public string? AccountNumber { get; set; }
    }
}
