using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public class CreatePriceRequest
{
    [Range(0.01, 999999999)]
    public decimal Price { get; set; }

    public int? DurationDays { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}

public class UpdatePriceRequest : CreatePriceRequest { }
