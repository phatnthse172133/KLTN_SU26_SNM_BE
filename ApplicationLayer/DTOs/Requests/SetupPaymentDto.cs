using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Requests
{
    public class SetupPaymentDto
    {
        public string MethodType { get; set; }
        public string TokenFromGateway { get; set; }
    }
}
