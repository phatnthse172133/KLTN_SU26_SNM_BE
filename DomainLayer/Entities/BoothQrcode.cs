using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Mã QR định danh gian hàng - khách hàng quét mã này để truy cập menu và đặt món trực tiếp tại bàn
/// </summary>
public partial class BoothQrcode
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public string QrcodeValue { get; set; } = null!;

    public string? QrcodeImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;
}
