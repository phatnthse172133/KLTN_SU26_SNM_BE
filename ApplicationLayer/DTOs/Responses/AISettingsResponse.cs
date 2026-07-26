using ApplicationLayer.Helppers;

namespace ApplicationLayer.DTOs.Responses;

public class AISettingsResponse
{
    public string Provider { get; set; } = string.Empty;
    public bool EnableExternalProvider { get; set; }
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
}
