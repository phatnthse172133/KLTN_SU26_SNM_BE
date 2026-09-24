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
        errors.AddRange(BoothSlotGeometry.Validate(layout, node, x, y, nodes, blocks));
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
        if (block.Type.Equals("Zone", StringComparison.OrdinalIgnoreCase)
            && !GeometryTolerance.IsZero(block.Rotation))
        {
            errors.Add($"Zone '{block.Name}' cannot be rotated. Zone rotation must be 0 degrees.");
            return errors;
        }
        if (!double.IsFinite(x) || !double.IsFinite(y)
            || !double.IsFinite(block.Width) || !double.IsFinite(block.Height)
            || block.Width <= 0 || block.Height <= 0)
        {
            errors.Add($"Zone '{block.Name}' must have finite coordinates and positive width and height.");
            return errors;
        }
        if (GeometryTolerance.IsNegative(x) || GeometryTolerance.IsNegative(y)
            || GeometryTolerance.Exceeds(x + block.Width, layout.Width)
            || GeometryTolerance.Exceeds(y + block.Height, layout.Height))
        {
            errors.Add($"Zone '{block.Name}' must remain completely inside the market boundary.");
        }

        var blockRight = x + block.Width;
        var blockBottom = y + block.Height;

        foreach (var other in otherBlocks.Where(b => b.Id != block.Id && !b.IsDeleted && b.LayoutId == layout.Id))
        {
            var otherRight = other.X + other.Width;
            var otherBottom = other.Y + other.Height;

            bool overlaps = GeometryTolerance.RectanglesHaveInteriorOverlap(
                x, y, block.Width, block.Height,
                other.X, other.Y, other.Width, other.Height);
            if (overlaps)
            {
                errors.Add($"Zone '{block.Name}' overlaps zone '{other.Name}'.");
                break;
            }
        }

        return errors;
    }
}
