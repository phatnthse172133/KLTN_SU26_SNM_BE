using System;
using System.Collections.Generic;

namespace ApplicationLayer.DTOs.Requests
{
    public class CreatePackagePolicyRequest
    {
        public string Title { get; set; } = null!;
        public List<string> Terms { get; set; } = new();
        public DateTimeOffset EffectiveFrom { get; set; }
    }
}
