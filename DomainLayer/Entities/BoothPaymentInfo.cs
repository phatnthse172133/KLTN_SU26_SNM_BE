using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Thông tin tài khoản/QR nhận thanh toán của gian hàng
/// </summary>
public partial class BoothPaymentInfo
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    /// <summary>
    /// BankTransfer | VNPay | MoMo | ZaloPay | Payos
    /// </summary>
    public string PaymentType { get; set; } = null!;

    public string? BankName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BankAccountHolder { get; set; }

    public string? QrimageUrl { get; set; }

    public bool IsDefault { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;
}
