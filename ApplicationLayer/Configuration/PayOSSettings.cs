namespace ApplicationLayer.Configuration
{
    public class PayOSSettings
    {
        public const string SectionName = "PayOS";

        public string ClientId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ChecksumKey { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
        // Optional role-specific pages. Keeping these separate lets the shared
        // payment provider return a Booth Owner to the Booth portal while the
        // Market Owner flow continues to use the market portal.
        public string BoothReturnUrl { get; set; } = string.Empty;
        public string BoothCancelUrl { get; set; } = string.Empty;
        public string WebhookUrl { get; set; } = string.Empty;
    }
}
