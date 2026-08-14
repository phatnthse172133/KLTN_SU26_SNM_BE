namespace ApplicationLayer.Common;

/// <summary>
/// Single source of truth for password length rules. Every DTO that accepts a
/// user-supplied or newly-set password (change password, reset password,
/// forgot password, registration, temporary passwords) must reference these
/// constants instead of hard-coding a length so the whole system stays in sync.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 6;
    public const int MaxLength = 128;

    public const string TooShortMessage = "New password must be at least 6 characters.";
}
