using System;

namespace ApplicationLayer.DTOs.Requests
{
    public class CreatePackagePolicyRequest
    {
        public string Version { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string ContentJson { get; set; } = null!;
        public string? ContentMarkdown { get; set; }
        public DateTimeOffset EffectiveFrom { get; set; }
    }
}
