namespace ApplicationLayer.DTOs.Responses;

public class FoodTagResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TagGroup { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsSelectable { get; set; }
    public bool IsPreferenceSelectable { get; set; }
    public bool IsAutoAssigned { get; set; }
}
