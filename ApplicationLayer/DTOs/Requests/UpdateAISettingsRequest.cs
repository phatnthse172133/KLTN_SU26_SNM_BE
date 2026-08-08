namespace ApplicationLayer.DTOs.Requests;

public class UpdateAISettingsRequest
{
    public string Provider { get; set; } = "Local";
    public bool EnableExternalProvider { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
