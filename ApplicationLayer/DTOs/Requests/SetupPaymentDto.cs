using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests
{
    public class SetupPaymentDto
    {
        public PaymentType MethodType { get; set; }
        [Required]
        public string TokenFromGateway { get; set; } = string.Empty;
    }
}
