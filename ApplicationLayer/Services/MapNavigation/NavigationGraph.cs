using DomainLayer.Entities;

namespace ApplicationLayer.Services.MapNavigation;

public sealed class NavigationGraph
{
    public IReadOnlyList<LayoutNode> Nodes { get; init; } = [];
    public IReadOnlyList<LayoutEdge> Edges { get; init; } = [];
    public IReadOnlyDictionary<string, LayoutNode> AccessBySlotCode { get; init; } =
        new Dictionary<string, LayoutNode>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> UnreachableSlotCodes { get; init; } = [];
    public IReadOnlyList<string> NarrowCorridorSlotCodes { get; init; } = [];
    public bool IsValid { get; init; } = true;
    public string? InvalidReason { get; init; }

    public static NavigationGraph Invalid(string reason) => new()
    {
        IsValid = false,
        InvalidReason = reason
    };

    public bool TryGetAccess(string? slotCode, out LayoutNode access)
    {
        access = null!;
        return !string.IsNullOrWhiteSpace(slotCode)
            && AccessBySlotCode.TryGetValue(slotCode, out access!);
    }
}
