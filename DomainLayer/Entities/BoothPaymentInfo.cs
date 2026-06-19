using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

// Thông tin tài khoản/QR nhận thanh toán của gian hàng
public partial class BoothPaymentInfo
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    // BankTransfer | VNPay | MoMo | ZaloPay | Payos
    public BoothPaymentType PaymentType { get; set; }

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
