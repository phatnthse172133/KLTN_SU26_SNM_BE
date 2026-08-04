using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class OrderPaymentStateMachineTests
{
    [Fact]
    public void PayOSOrder_CanOnlyProgressAfterVerifiedPaymentTransition()
    {
        var now = DateTime.UtcNow;
        var order = new Order { Status = OrderStatus.Placed, UpdatedAt = now };
        order.MarkPaymentPending(now);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.StartPreparing(now));
        order.MarkPaid(now);
        Assert.Equal(OrderStatus.Placed, order.Status);
        order.StartPreparing(now);
        order.MarkReadyForPickup(now);
        order.Complete(now);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.NotNull(order.CompletedAt);
    }

    [Fact]
    public void PaidPayment_CannotBeCancelledFailedOrExpired()
    {
        var now = DateTime.UtcNow;
        var payment = new Payment { Status = PaymentStatus.Pending, UpdatedAt = now };
        payment.MarkPaid(now);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Throws<InvalidOperationException>(() => payment.MarkCancelled(now));
        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed("FAIL", "failure", now));
        Assert.Throws<InvalidOperationException>(() => payment.MarkExpired(now));
    }

    [Fact]
    public void CashOrder_CanOnlyBeCancelledWhilePlaced()
    {
        var now = DateTime.UtcNow;
        var order = new Order { Status = OrderStatus.Placed, UpdatedAt = now };
        order.StartPreparing(now);
        Assert.Throws<InvalidOperationException>(() => order.Cancel(null, now));
    }
}
