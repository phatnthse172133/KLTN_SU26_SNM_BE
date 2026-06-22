using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Giấy tờ pháp lý của gian hàng để Admin xác minh trước khi cho phép hoạt động
/// </summary>
public partial class BoothDocument
{
    public Guid Id { get; set; }

    public Guid RegistrationId { get; set; }

    public string DocumentType { get; set; } = null!;

    /// <summary>
    /// Đường dẫn file giấy tờ đã upload
    /// </summary>
    public string FileUrl { get; set; } = null!;

    public DomainLayer.Enums.VerificationStatus VerificationStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual BoothRegistration Registration { get; set; } = null!;
}
