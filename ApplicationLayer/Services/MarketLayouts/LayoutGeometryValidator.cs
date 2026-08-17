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

        var block = blocks.FirstOrDefault(item =>
            item.Id == node.LayoutBlockId || (node.ZoneId.HasValue && item.ZoneId == node.ZoneId));
        if (block is null)
        {
            errors.Add("The booth slot must belong to a zone.");
            return errors;
        }

        var slotWidth = GetConfigNumber(block.ConfigJson, "boothWidth", 40);
        var slotHeight = GetConfigNumber(block.ConfigJson, "boothHeight", 40);
        var padding = GetConfigNumber(block.ConfigJson, "innerPadding", 0);
        var minX = block.X + padding + slotWidth / 2;
        var maxX = block.X + block.Width - padding - slotWidth / 2;
        var minY = block.Y + padding + slotHeight / 2;
        var maxY = block.Y + block.Height - padding - slotHeight / 2;
        if ((double)x < minX - 0.01 || (double)x > maxX + 0.01
            || (double)y < minY - 0.01 || (double)y > maxY + 0.01)
            errors.Add("The booth slot must stay inside its zone.");

        if (padding > 0)
        {
            var gapX = GetConfigNumber(block.ConfigJson, "horizontalGap", 0);
            var gapY = GetConfigNumber(block.ConfigJson, "verticalGap", 0);
            var pitchX = slotWidth + gapX;
            var pitchY = slotHeight + gapY;
            var snappedX = minX + Math.Round(((double)x - minX) / pitchX) * pitchX;
            var snappedY = minY + Math.Round(((double)y - minY) / pitchY) * pitchY;
            if (Math.Abs(snappedX - (double)x) > 0.01 || Math.Abs(snappedY - (double)y) > 0.01)
                errors.Add("The booth slot must align with the grid inside its zone.");
        }

        foreach (var other in nodes.Where(item =>
                     item.Id != node.Id && !item.IsDeleted && item.NodeType == LayoutNodeType.BoothSlot
                     && (item.LayoutBlockId == block.Id || item.ZoneId == block.ZoneId)))
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
        if (x < -0.01 || y < -0.01 || x + block.Width > (double)layout.Width + 0.01 || y + block.Height > (double)layout.Height + 0.01)
        {
            errors.Add($"Zone '{block.Name}' phải nằm hoàn toàn bên trong boundary khu chợ.");
        }

        var blockRight = x + block.Width;
        var blockBottom = y + block.Height;

        foreach (var other in otherBlocks.Where(b => b.Id != block.Id && !b.IsDeleted))
        {
            var otherRight = other.X + other.Width;
            var otherBottom = other.Y + other.Height;

            bool overlaps = !(blockRight <= other.X || x >= otherRight
                           || blockBottom <= other.Y || y >= otherBottom);
            if (overlaps)
            {
                errors.Add($"Zone '{block.Name}' bị chồng lấn với Zone '{other.Name}'.");
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
            return document.RootElement.TryGetProperty(property, out var value) && value.TryGetDouble(out var result)
                ? result
                : fallback;
        }
        catch (System.Text.Json.JsonException)
        {
            return fallback;
        }
    }
}
