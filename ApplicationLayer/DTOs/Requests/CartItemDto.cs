using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Requests
{
    public class CartItemDto
    {
        public Guid FoodItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Giá snapshot của món từ FE truyền lên để đối chiếu
    }
}
