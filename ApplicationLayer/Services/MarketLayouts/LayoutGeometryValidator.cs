using ApplicationLayer.DTOs.Requests;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.MarketLayouts;

public static class LayoutGeometryValidator
{
    public static IReadOnlyList<string> ValidateNodeMove(
        MarketLayout layout,
        LayoutNode node,
        decimal x,
        decimal y,
        IReadOnlyCollection<LayoutNode> nodes,
        IReadOnlyCollection<LayoutBlock> blocks)
    {
        var errors = new List<string>();
        if (x < 0 || y < 0 || x > layout.Width || y > layout.Height)
            errors.Add("The point must stay inside the market boundary.");

        if (node.NodeType != LayoutNodeType.BoothSlot)
            return errors;

        // An explicit block reference must never fall back to another layout's
        // block merely because both blocks share the market-level zone.
        var candidates = blocks.Where(item => !item.IsDeleted && item.LayoutId == layout.Id).ToList();
        if (!node.LayoutBlockId.HasValue
            && candidates.Count(item => node.ZoneId.HasValue && item.ZoneId == node.ZoneId) > 1)
        {
            errors.Add("Select the specific zone block for this booth slot.");
            return errors;
        }
        var block = node.LayoutBlockId.HasValue
            ? candidates.FirstOrDefault(item => item.Id == node.LayoutBlockId.Value)
            : candidates.SingleOrDefault(item => node.ZoneId.HasValue && item.ZoneId == node.ZoneId);
        if (block is null)
        {
            errors.Add("The booth slot must belong to a zone.");
            return errors;
        }

        if (node.ZoneId.HasValue && node.ZoneId != block.ZoneId)
        {
            errors.Add("The booth slot and its block must belong to the same zone.");
            return errors;
        }

        var slotWidth = GetConfigNumber(block.ConfigJson, "boothWidth", 40);
        var slotHeight = GetConfigNumber(block.ConfigJson, "boothHeight", 40);
        var padding = GetConfigNumber(block.ConfigJson, "innerPadding", 0);
        if (!double.IsFinite(slotWidth) || !double.IsFinite(slotHeight)
            || slotWidth <= 0 || slotHeight <= 0 || !double.IsFinite(padding) || padding < 0)
        {
            errors.Add("Booth dimensions must be positive and zone padding must be non-negative.");
            return errors;
        }
        var minX = block.X + padding + slotWidth / 2;
        var maxX = block.X + block.Width - padding - slotWidth / 2;
        var minY = block.Y + padding + slotHeight / 2;
        var maxY = block.Y + block.Height - padding - slotHeight / 2;
        if ((double)x < minX - 0.01 || (double)x > maxX + 0.01
            || (double)y < minY - 0.01 || (double)y > maxY + 0.01)
            errors.Add("The booth slot must stay inside its zone.");

        // Grid snapping is an editor aid. Generated rows may be centered and
        // manual designs remain valid when their footprints fit without overlap.

        foreach (var other in nodes.Where(item =>
                     item.Id != node.Id && !item.IsDeleted && item.NodeType == LayoutNodeType.BoothSlot
                     && item.LayoutId == layout.Id
                     && (item.LayoutBlockId == block.Id
                         || (!item.LayoutBlockId.HasValue && block.ZoneId.HasValue && item.ZoneId == block.ZoneId))))
        {
            var otherX = (double)other.Xcoordinate;
            var otherY = (double)other.Ycoordinate;
            if (Math.Abs((double)x - otherX) < slotWidth
                && Math.Abs((double)y - otherY) < slotHeight)
            {
                errors.Add($"The booth slot overlaps '{other.SlotCode ?? other.NodeName ?? "another slot"}'.");
                break;
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateBlockMove(
        MarketLayout layout,
        LayoutBlock block,
        double x,
        double y,
        IReadOnlyCollection<LayoutBlock> otherBlocks)
    {
        var errors = new List<string>();
        if (!double.IsFinite(x) || !double.IsFinite(y)
            || !double.IsFinite(block.Width) || !double.IsFinite(block.Height)
            || block.Width <= 0 || block.Height <= 0)
        {
            errors.Add($"Zone '{block.Name}' must have finite coordinates and positive width and height.");
            return errors;
        }
        if (x < -0.01 || y < -0.01 || x + block.Width > (double)layout.Width + 0.01 || y + block.Height > (double)layout.Height + 0.01)
        {
            errors.Add($"Zone '{block.Name}' must remain completely inside the market boundary.");
        }

        var blockRight = x + block.Width;
        var blockBottom = y + block.Height;

        foreach (var other in otherBlocks.Where(b => b.Id != block.Id && !b.IsDeleted && b.LayoutId == layout.Id))
        {
            var otherRight = other.X + other.Width;
            var otherBottom = other.Y + other.Height;

            bool overlaps = !(blockRight <= other.X || x >= otherRight
                           || blockBottom <= other.Y || y >= otherBottom);
            if (overlaps)
            {
                errors.Add($"Zone '{block.Name}' overlaps zone '{other.Name}'.");
                break;
            }
        }

        return errors;
    }

    private static double GetConfigNumber(string? json, string property, double fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty(property, out var value)
                && value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetDouble(out var result)
                ? result
                : fallback;
        }
        catch (System.Text.Json.JsonException)
        {
            return fallback;
        }
    }
}
