using System;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Admin
{
    public class AILogDto
    {
        public Guid Id { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }

        public Guid? NightMarketId { get; set; }
        public string? NightMarketName { get; set; }

        public AIRecommendationType RecommendationType { get; set; }
        public string RecommendationTypeName => RecommendationType.ToString();

        public string InputJson { get; set; } = string.Empty;
        public string? ParsedIntentJson { get; set; }
        public string ResultJson { get; set; } = string.Empty;
        public string? SelectedOptionId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
