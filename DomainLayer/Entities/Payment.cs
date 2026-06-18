using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Lịch sử giao dịch thanh toán/hoàn tiền - tích hợp đa cổng VNPay/ZaloPay/MoMo/Payos
/// </summary>
public partial class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>
    /// Payment: thu tiền | Refund: hoàn tiền
    /// </summary>
    public string Type { get; set; } = null!;

    public string Gateway { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string Status { get; set; } = null!;

    /// <summary>
    /// Mã tham chiếu từ cổng thanh toán bên thứ 3 - dùng để tra soát/khiếu nại
    /// </summary>
    public string? GatewayRef { get; set; }

    public string? RefundReason { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
