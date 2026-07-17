using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Responses
{
    public class RejectOrderDto
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = null!; // Lý do hủy đơn (ví dụ: Hết món, quán đóng cửa...)
    }
}
