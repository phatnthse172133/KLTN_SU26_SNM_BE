using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests
{
    public class SetupPaymentDto
    {
        public PaymentType MethodType { get; set; }
        public string TokenFromGateway { get; set; }
    }
}
