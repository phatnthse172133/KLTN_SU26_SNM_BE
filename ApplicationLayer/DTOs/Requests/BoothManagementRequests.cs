using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class UpdateMyBoothRequest
{
    [Required, StringLength(200)] public string BoothName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Phone, StringLength(20)] public string? PhoneNumber { get; set; }
    [Url, StringLength(500)] public string? ThumbnailUrl { get; set; }
    [Url, StringLength(500)] public string? PaymentQrImage { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
}

public class AdminUpdateBoothRequest : UpdateMyBoothRequest
{
    public Guid? ZoneId { get; set; }
    [StringLength(50)] public string? SlotNumber { get; set; }
    public decimal? MapPositionX { get; set; }
    public decimal? MapPositionY { get; set; }
    public BoothStatus Status { get; set; }
    public bool IsFeatured { get; set; }
}
