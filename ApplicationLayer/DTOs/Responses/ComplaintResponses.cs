namespace ApplicationLayer.DTOs.Responses;

public class ComplaintResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid BoothId { get; set; }
    public Guid OrderId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AdminResponse { get; set; }
    public string? EvidenceRequestNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResolutionAction { get; set; }
    public string? PolicyViolation { get; set; }
    public bool CanWithdraw { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public List<ComplaintStatusHistoryResponse> StatusHistory { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ComplaintStatusHistoryResponse
{
    public Guid Id { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorRole { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ComplaintCountsResponse
{
    public int Pending { get; set; }
    public int Resolved { get; set; }
    public int Rejected { get; set; }
    public int Total { get; set; }
}

public class ComplaintImageUploadResponse
{
    public string ImageUrl { get; set; } = string.Empty;
}
