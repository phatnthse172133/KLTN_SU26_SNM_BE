using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class UpdateMyBoothRequest
{
    [Required, StringLength(200)] public string BoothName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(20)] public string? PhoneNumber { get; set; }
    [StringLength(500)] public string? ThumbnailUrl { get; set; }
    [StringLength(500)] public string? PaymentQrImage { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
}

public class AdminUpdateBoothRequest : UpdateMyBoothRequest
{
    public Guid? ZoneId { get; set; }
    [StringLength(50)] public string? SlotNumber { get; set; }
    public decimal? MapPositionX { get; set; }
    public decimal? MapPositionY { get; set; }
    public bool IsFeatured { get; set; }
}

public class MarketOwnerCreateBoothRequest
{
    [Required] public Guid BoothOwnerId { get; set; }
    [Required, StringLength(200)] public string BoothName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(20)] public string? PhoneNumber { get; set; }
    [StringLength(500)] public string? ThumbnailUrl { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
}

public class MarketOwnerUpdateBoothRequest
{
    [Required, StringLength(200)] public string BoothName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(20)] public string? PhoneNumber { get; set; }
    [StringLength(500)] public string? ThumbnailUrl { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
}

public class MarketOwnerChangeBoothStatusRequest
{
    [Required]
    [EnumDataType(typeof(BoothStatus))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BoothStatus Status { get; set; }
}
