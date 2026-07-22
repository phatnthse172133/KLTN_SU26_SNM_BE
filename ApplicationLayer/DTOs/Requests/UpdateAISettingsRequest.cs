namespace ApplicationLayer.DTOs.Requests;

public class UpdateAISettingsRequest
{
    public string Provider { get; set; } = "Gemini";
    public bool EnableExternalProvider { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-1.5-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
