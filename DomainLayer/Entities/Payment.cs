using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Lịch sử giao dịch thanh toán/hoàn tiền - tích hợp đa cổng VNPay/ZaloPay/MoMo/Payos
public partial class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    // FK tới User – chủ gian hàng nhận tiền
    public Guid BoothOwnerId { get; set; }

    public PaymentType Type { get; set; }

    public string Gateway { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public PaymentStatus Status { get; set; }

    // Mã tham chiếu từ cổng thanh toán bên thứ 3 - dùng để tra soát/khiếu nại
    public string? GatewayRef { get; set; }

    public string? RefundReason { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation: chủ gian hàng nhận tiền
    public virtual User BoothOwner { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}