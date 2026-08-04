namespace ApplicationLayer.Helppers;

public static class TextHelper
{
    public static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
