using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Giấy tờ pháp lý của gian hàng để Admin xác minh trước khi cho phép hoạt động
/// </summary>
public partial class BoothDocument
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string DocumentUrl { get; set; } = null!;

    /// <summary>
    /// Pending: chờ duyệt | Verified: đã xác minh | Rejected: bị từ chối
    /// </summary>
    public string VerificationStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;
}
