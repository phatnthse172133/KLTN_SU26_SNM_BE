using System.Text.Json.Serialization;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Serialization;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CheckoutCartBoothRequest
{
    public Guid CheckoutRequestId { get; set; }
    public Guid BoothId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentType PaymentMethod { get; set; } = PaymentType.PayOS;
    public string? PromotionCode { get; set; }
    public string? Note { get; set; }
}

public sealed class CreateCustomerOrderRequest
{
    [JsonConverter(typeof(UpperSnakeCaseEnumConverter<PaymentType>))]
    public PaymentType PaymentMethod { get; set; }
    public Guid? PromotionId { get; set; }
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class CancelCustomerOrderRequest
{
    public string? Reason { get; set; }
}
