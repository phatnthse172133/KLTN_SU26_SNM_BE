namespace ApplicationLayer.AI.DTOs;

public class FoodIntentDto
{
    public IReadOnlyCollection<string> MatchedTagNames { get; set; } = [];
    public IReadOnlyCollection<string> AvoidTagNames { get; set; } = [];
    public decimal? BudgetMax { get; set; }
    public string? DiningStyle { get; set; }
}

public class ExplanationContextDto
{
    public string Type { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public IReadOnlyCollection<string> MatchedTags { get; set; } = [];
    public decimal? Price { get; set; }
    public decimal? BudgetMax { get; set; }
    public decimal? Rating { get; set; }
    public string? NightMarketName { get; set; }
}
