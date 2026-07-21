namespace ApplicationLayer.DTOs.Responses;

public class ReviewResponse
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public string? BoothName { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public short Rating { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsVisible { get; set; }
    public ReviewReplyResponse? Reply { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ReviewReplyResponse
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public Guid BoothOwnerId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
