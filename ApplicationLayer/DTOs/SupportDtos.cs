using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.DTOs;

public class CreateSupportTicketRequest
{
    [Required, StringLength(200, MinimumLength = 5)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Category { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Description { get; set; } = string.Empty;
    public Guid? BoothId { get; set; }
    public Guid? NightMarketId { get; set; }
    [StringLength(1000)] public string? PageUrl { get; set; }
}

public class SendSupportMessageRequest
{
    [Required, StringLength(4000, MinimumLength = 1)] public string Body { get; set; } = string.Empty;
    public bool IsInternalNote { get; set; }
}

public class UpdateSupportTicketRequest
{
    [Required, StringLength(30)] public string Status { get; set; } = string.Empty;
    public Guid? AssignedAdminId { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
}

public class SupportTicketQuery : PaginationReq
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string? RequesterRole { get; set; }
    public bool? Overdue { get; set; }
}

public class SupportTicketListItem
{
    public Guid Id { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string RequesterRole { get; set; } = string.Empty;
    public DateTime DueAt { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SupportMessageResponse
{
    public Guid Id { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsInternalNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupportAttachmentResponse
{
    public Guid Id { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class SupportStatusHistoryResponse
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SupportTicketDetail : SupportTicketListItem
{
    public string Description { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
    public Guid? BoothId { get; set; }
    public Guid? NightMarketId { get; set; }
    public Guid? AssignedAdminId { get; set; }
    public string? AssignedAdminName { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public IReadOnlyCollection<SupportMessageResponse> Messages { get; set; } = [];
    public IReadOnlyCollection<SupportAttachmentResponse> Attachments { get; set; } = [];
    public IReadOnlyCollection<SupportStatusHistoryResponse> StatusHistory { get; set; } = [];
}

public class SupportMetricsResponse
{
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int WaitingForRequester { get; set; }
    public int Overdue { get; set; }
    public int ResolvedToday { get; set; }
}
