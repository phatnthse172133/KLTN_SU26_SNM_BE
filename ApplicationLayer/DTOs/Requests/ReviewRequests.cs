using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public class CreateReviewRequest
{
    public Guid BoothId { get; set; }
    public Guid OrderId { get; set; }

    [Range(1, 5)]
    public short Rating { get; set; }

    [StringLength(2000)]
    public string? Content { get; set; }

    [Url, StringLength(500)]
    public string? ImageUrl { get; set; }
}

public class UpdateReviewVisibilityRequest
{
    public bool IsVisible { get; set; }
}

public class UpsertReviewReplyRequest
{
    [Required, StringLength(2000)]
    public string Content { get; set; } = string.Empty;
}
