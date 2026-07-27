using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public class CreateReviewRequest
{
    // Kept for backward compatibility; the service derives the authoritative booth from OrderId.
    public Guid BoothId { get; set; }
    public Guid OrderId { get; set; }

    [Range(1, 5)]
    public short Rating { get; set; }

    [StringLength(2000)]
    public string? Content { get; set; }

    [Url, StringLength(500)]
    public string? ImageUrl { get; set; }
}

public class UpdateReviewRequest
{
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

public class AdminReviewQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public short? Rating { get; set; }
    public bool? IsVisible { get; set; }
    public Guid? BoothId { get; set; }
    public string? Keyword { get; set; }
}
