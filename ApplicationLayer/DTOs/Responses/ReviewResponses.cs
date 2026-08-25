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

public class CustomerReviewHistoryResponse
{
    public Guid ReviewId { get; set; }
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid BoothId { get; set; }
    public string? BoothName { get; set; }
    public short Rating { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsVisible { get; set; }
    public bool HasReply { get; set; }
    public CustomerReviewReplyResponse? Reply { get; set; }
    public bool CanEdit { get; set; }
    public DateTime? EditDeadline { get; set; }
    public List<CustomerFoodReviewHistoryResponse> FoodReviews { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FoodReviewResponse
{
    public Guid Id { get; set; }
    public Guid OrderDetailId { get; set; }
    public Guid OrderId { get; set; }
    public Guid FoodItemId { get; set; }
    public string? FoodItemName { get; set; }
    public Guid BoothId { get; set; }
    public string? BoothName { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public short Rating { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsVisible { get; set; }
    /// <summary>Derived from FoodReview→Order ownership; not a stored fake flag.</summary>
    public bool IsVerifiedPurchase { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CustomerFoodReviewHistoryResponse
{
    public Guid FoodReviewId { get; set; }
    public Guid OrderDetailId { get; set; }
    public Guid OrderId { get; set; }
    public string? OrderCode { get; set; }
    public Guid FoodItemId { get; set; }
    public string? FoodItemName { get; set; }
    public Guid BoothId { get; set; }
    public string? BoothName { get; set; }
    public short Rating { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsVisible { get; set; }
    public bool CanEdit { get; set; }
    public DateTime? EditDeadline { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ReviewImageUploadResponse
{
    public string ImageUrl { get; set; } = string.Empty;
}
