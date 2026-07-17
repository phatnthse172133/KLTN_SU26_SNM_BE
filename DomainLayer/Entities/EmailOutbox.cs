using System;

namespace DomainLayer.Entities;

public class EmailOutbox
{
    public Guid Id { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    public string EmailType { get; set; } = string.Empty;

    public Guid ReferenceId { get; set; }

    public string Status { get; set; } = "Pending";

    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
