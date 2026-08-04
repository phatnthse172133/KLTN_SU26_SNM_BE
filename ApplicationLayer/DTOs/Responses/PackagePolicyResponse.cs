using System;
using System.Collections.Generic;

namespace ApplicationLayer.DTOs.Responses
{
    public class PackagePolicyResponse
    {
        public Guid Id { get; set; }
        public Guid PackageId { get; set; }
        public string Version { get; set; } = null!;
        public string DisplayVersion { get; set; } = null!;
        public string Title { get; set; } = null!;
        public List<string> Terms { get; set; } = new();
        public DateTime EffectiveFrom { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
