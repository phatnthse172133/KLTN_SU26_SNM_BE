using System.Text.Json.Serialization;
using static DomainLayer.Enums.GeneralEnum;

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
