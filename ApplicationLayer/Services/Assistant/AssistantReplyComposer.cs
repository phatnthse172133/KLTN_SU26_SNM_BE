using DomainLayer.Enums;

namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantReplyComposer
{
    public string Compose(
        ParsedAssistantIntent intent,
        IReadOnlyList<AssistantScoredFood> recommendations,
        IReadOnlyList<AssistantMealPlanDraft> mealPlans)
    {
        if (intent.Intent is AssistantIntentKind.CHITCHAT or AssistantIntentKind.CLARIFY)
            return string.IsNullOrWhiteSpace(intent.AssistantReply)
                ? "Mình có thể gợi ý món chợ đêm nếu bạn nói khẩu vị, ngân sách hoặc số người."
                : intent.AssistantReply!;

        if (intent.Intent == AssistantIntentKind.MEAL_PLAN)
        {
            if (mealPlans.Count == 0)
                return "Mình chưa soạn được thực đơn hợp lệ từ món đang bán và ngân sách hiện có. Mình không bịa món, giá hoặc tổng tiền.";

            var first = mealPlans[0];
            var names = string.Join(", ", first.Items.Select(item => $"{item.FoodItem.Name} x{item.Quantity}"));
            var total = first.EstimatedTotal.ToString("N0");
            var budget = first.BudgetMax is null ? string.Empty : $" trong ngân sách {first.BudgetMax:N0}đ";
            var countNote = mealPlans.Count == 1
                ? string.Empty
                : $" Có {mealPlans.Count} phương án khác nhau.";
            var title = string.IsNullOrWhiteSpace(first.Title) ? string.Empty : $" ({first.Title})";
            var unknown = mealPlans.SelectMany(plan => plan.UnknownDataFacets).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var unknownNote = UnknownNote(unknown);
            return $"Mình soạn thực đơn{title} tại {first.NightMarketName} cho {first.PartySize} người{budget}: {names}. Dự kiến {total}đ.{countNote}{unknownNote}";
        }

        if (recommendations.Count == 0)
            return "Hiện chưa có món nào khớp ràng buộc cứng (dị ứng, kiêng, ngân sách, chợ đang mở). Bạn thử nới điều kiện hoặc chọn chợ khác nhé.";

        var top = recommendations.Take(3).Select(item => item.Eligible.FoodItem.Name);
        return $"Mình gợi ý: {string.Join(", ", top)}.{UnknownNote(recommendations.SelectMany(item => item.UnknownDataFacets).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())}";
    }

    private static string UnknownNote(IReadOnlyList<string> unknown)
    {
        if (unknown.Count == 0)
            return string.Empty;
        if (unknown.Any(item => string.Equals(item, "sales", StringComparison.OrdinalIgnoreCase)))
            return " Một số tiêu chí (ví dụ lượng bán, calories) không đủ dữ liệu nên mình không bịa.";
        return " Một số tiêu chí (ví dụ calories, độ dầu) không có trong dữ liệu món nên mình không bịa.";
    }
}
