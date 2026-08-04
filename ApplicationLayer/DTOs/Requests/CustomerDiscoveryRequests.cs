using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CustomerBoothQueryRequest : PaginationReq
{
    public Guid? MarketId { get; set; }

    [MaxLength(200)]
    public string? Search { get; set; }

    public bool? OpenNow { get; set; }

    [Range(0, 5)]
    public decimal? MinimumRating { get; set; }

    [RegularExpression("^(featured|name|rating)$", ErrorMessage = "Sort must be featured, name, or rating.")]
    public string Sort { get; set; } = "featured";
}

public sealed class CustomerFoodQueryRequest : PaginationReq
{
    public Guid? MarketId { get; set; }
    public Guid? BoothId { get; set; }
    public Guid? CategoryId { get; set; }

    [MaxLength(200)]
    public string? Search { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? MinPrice { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? MaxPrice { get; set; }

    public bool? AvailableOnly { get; set; }

    [RegularExpression("^(featured|name|priceAsc|priceDesc)$", ErrorMessage = "Sort must be featured, name, priceAsc, or priceDesc.")]
    public string Sort { get; set; } = "featured";
}
