using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Giấy tờ pháp lý của gian hàng để Admin xác minh trước khi cho phép hoạt động
public partial class BoothDocument
{
    public Guid Id { get; set; }

    public Guid? RegistrationId { get; set; }

    public Guid? BoothId { get; set; }

    public BoothDocumentType DocumentType { get; set; }

    public string DocumentUrl { get; set; } = null!;

    // Đường dẫn file giấy tờ đã upload
    public string FileUrl { get; set; } = null!;

    public BoothDocumentStatus VerificationStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual BoothRegistration? Registration { get; set; }

    public virtual Booth? Booth { get; set; }
}
