using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

// Log tối giản cho AI recommendation: input, intent đã parse, kết quả tóm tắt và option user chọn.
public partial class AIRecommendationLog
{
    public Guid Id { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? NightMarketId { get; set; }

    public AIRecommendationType RecommendationType { get; set; }

    public string InputJson { get; set; } = null!;

    public string? ParsedIntentJson { get; set; }

    public string ResultJson { get; set; } = null!;

    public string? SelectedOptionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? Customer { get; set; }

    public virtual NightMarket? NightMarket { get; set; }
}
