using System.Text.Json;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

public readonly record struct BoothSlotFootprint(
    double Width,
    double Height,
    double InnerPadding);

/// <summary>
/// Canonical BoothSlot ownership, footprint, containment and overlap rules.
/// All values are local layout units; generated physical metadata has already
/// been converted with the layout's independent X/Y calibration.
/// </summary>
public static class BoothSlotGeometry
{
    public static IReadOnlyList<string> Validate(
        MarketLayout layout,
        LayoutNode node,
        decimal x,
        decimal y,
        IReadOnlyCollection<LayoutNode> nodes,
        IReadOnlyCollection<LayoutBlock> blocks)
    {
        var errors = new List<string>();
        if (node.NodeType != LayoutNodeType.BoothSlot)
            return errors;

        if (!TryResolveOwningBlock(layout.Id, node, blocks, out var block, out var ownershipError))
        {
            errors.Add(ownershipError!);
            return errors;
        }

        if (!block!.ZoneId.HasValue || !node.ZoneId.HasValue || node.ZoneId != block.ZoneId)
        {
            errors.Add("The booth slot Zone and LayoutBlock must identify the same zone.");
            return errors;
        }

        if (!TryResolveFootprint(block, out var footprint, out var footprintError))
        {
            errors.Add(footprintError!);
            return errors;
        }

        if (!double.IsFinite(block.X) || !double.IsFinite(block.Y)
            || !double.IsFinite(block.Width) || !double.IsFinite(block.Height)
            || block.Width <= 0 || block.Height <= 0)
        {
            errors.Add($"Zone '{block.Name}' must have finite coordinates and positive dimensions.");
            return errors;
        }

        var centerX = (double)x;
        var centerY = (double)y;
        var left = centerX - footprint.Width / 2;
        var right = centerX + footprint.Width / 2;
        var top = centerY - footprint.Height / 2;
        var bottom = centerY + footprint.Height / 2;
        var validLeft = block.X + footprint.InnerPadding;
        var validRight = block.X + block.Width - footprint.InnerPadding;
        var validTop = block.Y + footprint.InnerPadding;
        var validBottom = block.Y + block.Height - footprint.InnerPadding;

        if (GeometryTolerance.IsNegative(left)
            || GeometryTolerance.IsNegative(top)
            || GeometryTolerance.Exceeds(right, layout.Width)
            || GeometryTolerance.Exceeds(bottom, layout.Height)
            || validLeft - left > GeometryTolerance.Epsilon
            || right - validRight > GeometryTolerance.Epsilon
            || validTop - top > GeometryTolerance.Epsilon
            || bottom - validBottom > GeometryTolerance.Epsilon)
        {
            errors.Add("The full booth slot footprint must stay inside its zone after inner padding.");
        }

        foreach (var other in nodes.Where(item =>
                     item.Id != node.Id
                     && !item.IsDeleted
                     && item.LayoutId == layout.Id
                     && item.NodeType == LayoutNodeType.BoothSlot))
        {
            if (!TryResolveOwningBlock(layout.Id, other, blocks, out var otherBlock, out var otherOwnershipError))
            {
                errors.Add($"Cannot validate booth slot '{DisplayName(other)}': {otherOwnershipError}");
                break;
            }
            if (!TryResolveFootprint(otherBlock!, out var otherFootprint, out var otherFootprintError))
            {
                errors.Add($"Cannot validate booth slot '{DisplayName(other)}': {otherFootprintError}");
                break;
            }

            var otherLeft = (double)other.Xcoordinate - otherFootprint.Width / 2;
            var otherTop = (double)other.Ycoordinate - otherFootprint.Height / 2;
            if (GeometryTolerance.RectanglesHaveInteriorOverlap(
                    left, top, footprint.Width, footprint.Height,
                    otherLeft, otherTop, otherFootprint.Width, otherFootprint.Height))
            {
                errors.Add($"The booth slot overlaps '{DisplayName(other)}'.");
                break;
            }
        }

        return errors;
    }

    public static bool TryResolveOwningBlock(
        Guid layoutId,
        LayoutNode node,
        IReadOnlyCollection<LayoutBlock> blocks,
        out LayoutBlock? block,
        out string? error)
    {
        var candidates = blocks
            .Where(item => !item.IsDeleted && item.LayoutId == layoutId)
            .ToList();

        if (node.LayoutBlockId.HasValue)
        {
            block = candidates.FirstOrDefault(item => item.Id == node.LayoutBlockId.Value);
            error = block is null
                ? "The selected LayoutBlock does not belong to this layout."
                : null;
            return block is not null;
        }

        if (!node.ZoneId.HasValue)
        {
            block = null;
            error = "Select a zone block before adding or moving a booth slot.";
            return false;
        }

        var zoneMatches = candidates.Where(item => item.ZoneId == node.ZoneId).ToList();
        if (zoneMatches.Count != 1)
        {
            block = null;
            error = zoneMatches.Count == 0
                ? "No LayoutBlock represents the booth slot's zone in this layout."
                : "The booth slot's legacy ZoneId maps to multiple LayoutBlocks; select a specific block.";
            return false;
        }

        block = zoneMatches[0];
        error = null;
        return true;
    }

    public static bool TryResolveFootprint(
        LayoutBlock block,
        out BoothSlotFootprint footprint,
        out string? error)
    {
        footprint = default;
        if (string.IsNullOrWhiteSpace(block.ConfigJson))
        {
            error = $"Zone '{block.Name}' has no configured booth footprint.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(block.ConfigJson);
            var root = document.RootElement;
            if (!TryGetNumber(root, "boothWidth", out var width)
                || !TryGetNumber(root, "boothHeight", out var height)
                || !TryGetNumber(root, "innerPadding", out var padding)
                || !double.IsFinite(width) || width <= 0
                || !double.IsFinite(height) || height <= 0
                || !double.IsFinite(padding) || padding < 0)
            {
                error = $"Zone '{block.Name}' must define positive boothWidth/boothHeight and non-negative innerPadding.";
                return false;
            }

            footprint = new BoothSlotFootprint(width, height, padding);
            error = null;
            return true;
        }
        catch (JsonException)
        {
            error = $"Zone '{block.Name}' has invalid booth footprint metadata.";
            return false;
        }
    }

    private static bool TryGetNumber(JsonElement root, string property, out double value)
    {
        value = default;
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out value);
    }

    private static string DisplayName(LayoutNode node)
        => node.SlotCode ?? node.NodeName ?? node.Id.ToString();
}
