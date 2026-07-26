using System;

namespace ApplicationLayer.DTOs.Responses;

public class UserStatusHistoryResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid ChangedByAdminId { get; set; }

    public string ChangedByAdminName { get; set; } = string.Empty;

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
