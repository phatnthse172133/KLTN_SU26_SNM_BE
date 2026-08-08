namespace ApplicationLayer.Helppers;

public static class TextHelper
{
    public static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? NormalizePhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var cleaned = new string(phone.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '.' && c != '(' && c != ')' && c != '/').ToArray());

        if (cleaned.StartsWith("+84"))
        {
            cleaned = "0" + cleaned.Substring(3);
        }
        else if (cleaned.StartsWith("84") && cleaned.Length == 11 && cleaned.Length > 2 && "235789".Contains(cleaned[2]))
        {
            cleaned = "0" + cleaned.Substring(2);
        }

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    public static bool IsValidPhoneNumber(string? phone)
    {
        var normalized = NormalizePhoneNumber(phone);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        return System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^0[235789]\d{8}$");
    }
}
