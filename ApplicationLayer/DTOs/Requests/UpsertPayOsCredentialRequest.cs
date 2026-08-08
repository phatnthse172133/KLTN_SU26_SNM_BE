using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Requests
{
    public class UpsertPayOsCredentialRequest
    {
        [Required(ErrorMessage = "BoothId không được để trống.")]
        public Guid BoothId { get; set; }

        //[Required(ErrorMessage = "ClientId không được để trống.")]
        public string ClientId { get; set; }

        //[Required(ErrorMessage = "ApiKey không được để trống.")]
        public string ApiKey { get; set; }

        //[Required(ErrorMessage = "ChecksumKey không được để trống.")]
        public string ChecksumKey { get; set; }

        //[Required(ErrorMessage = "PayoutClientId không được để trống.")]
        public string PayoutClientId { get; set; }

        //[Required(ErrorMessage = "PayoutApiKey không được để trống.")]
        public string PayoutApiKey { get; set; }

        //[Required(ErrorMessage = "PayoutChecksumKey không được để trống.")]
        public string PayoutChecksumKey { get; set; }
    }
}
