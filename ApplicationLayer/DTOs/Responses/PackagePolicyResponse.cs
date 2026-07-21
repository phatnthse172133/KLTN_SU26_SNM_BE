using System;

namespace ApplicationLayer.DTOs.Responses
{
    public class PackagePolicyResponse
    {
        public Guid Id { get; set; }
        public Guid PackageId { get; set; }
        public string Version { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string ContentJson { get; set; } = null!;
        public string? ContentMarkdown { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
