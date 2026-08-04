using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class SaveNavigationAnchorRequest
{
    [Required] public Guid LayoutNodeId { get; set; }
    [Required] public NavigationAnchorType AnchorType { get; set; }
    [Required, StringLength(50)] public string AnchorCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string DisplayName { get; set; } = string.Empty;
    [Range(-90, 90)] public decimal Latitude { get; set; }
    [Range(-180, 180)] public decimal Longitude { get; set; }
    public bool IsCustomerAccessible { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
}
