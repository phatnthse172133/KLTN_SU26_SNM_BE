namespace ApplicationLayer.Helppers;

public class ErrorResponse
{
    public string TraceId { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string? Details { get; set; }
}
