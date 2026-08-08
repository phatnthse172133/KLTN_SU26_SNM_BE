using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Responses
{
    public class BoothPayOsStatusResponse
    {
        public bool IsPayInConfigured { get; set; }
        //public string MaskedPayInClientId { get; set; } = string.Empty;

        public bool IsPayOutConfigured { get; set; }
        //public string MaskedPayOutClientId { get; set; } = string.Empty;
        //public bool IsConfigured { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
