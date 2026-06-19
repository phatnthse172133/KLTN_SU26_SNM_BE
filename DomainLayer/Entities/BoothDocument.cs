using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Giấy tờ pháp lý của gian hàng để Admin xác minh trước khi cho phép hoạt động
public partial class BoothDocument
{
    public Guid Id { get; set; }

    public Guid RegistrationId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string DocumentUrl { get; set; } = null!;

    // Pending: chờ duyệt | Verified: đã xác minh | Rejected: bị từ chối
    public BoothDocumentStatus VerificationStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual BoothRegistration Registration { get; set; } = null!;
}
