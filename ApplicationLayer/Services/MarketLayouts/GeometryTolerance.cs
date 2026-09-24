namespace ApplicationLayer.Services.MarketLayouts;

/// <summary>
/// Numerical tolerance for floating-point geometry only. This is deliberately
/// far smaller than any business distance: one centimetre must never be
/// treated as boundary or overlap noise.
/// </summary>
public static class GeometryTolerance
{
    public const double Epsilon = 1e-9;

    public static bool Exceeds(double value, double limit)
        => value - limit > Epsilon;

    public static bool IsNegative(double value)
        => value < -Epsilon;

    public static bool IsZero(double value)
        => Math.Abs(value) <= Epsilon;

    public static bool RectanglesHaveInteriorOverlap(
        double leftX, double leftY, double leftWidth, double leftHeight,
        double rightX, double rightY, double rightWidth, double rightHeight)
    {
        var overlapX = Math.Min(leftX + leftWidth, rightX + rightWidth)
            - Math.Max(leftX, rightX);
        var overlapY = Math.Min(leftY + leftHeight, rightY + rightHeight)
            - Math.Max(leftY, rightY);
        return overlapX > Epsilon && overlapY > Epsilon;
    }
}
