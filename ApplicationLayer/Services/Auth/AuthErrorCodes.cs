namespace ApplicationLayer.Services.Auth;

/// <summary>
/// Stable error codes returned by authentication and credential-management flows.
/// Keep messages user-friendly; clients should branch on these codes when necessary.
/// </summary>
public static class AuthErrorCodes
{
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string UserNameAlreadyExists = "USERNAME_ALREADY_EXISTS";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string GooglePasswordLoginNotAllowed = "GOOGLE_PASSWORD_LOGIN_NOT_ALLOWED";
    public const string GoogleAuthUnavailable = "GOOGLE_AUTH_UNAVAILABLE";
    public const string InvalidGoogleToken = "INVALID_GOOGLE_TOKEN";
    public const string GoogleAccountLinkRequired = "GOOGLE_ACCOUNT_LINK_REQUIRED";
    public const string GoogleCustomerOnly = "GOOGLE_CUSTOMER_ONLY";
    public const string InvalidOrExpiredVerificationToken = "INVALID_OR_EXPIRED_VERIFICATION_TOKEN";
    public const string InvalidOrExpiredResetOtp = "INVALID_OR_EXPIRED_RESET_OTP";
    public const string PasswordResetNotAllowed = "PASSWORD_RESET_NOT_ALLOWED";
    public const string PasswordReuseNotAllowed = "PASSWORD_REUSE_NOT_ALLOWED";
    public const string InvalidOrExpiredResetToken = "INVALID_OR_EXPIRED_RESET_TOKEN";
    public const string InvalidOrExpiredRefreshToken = "INVALID_OR_EXPIRED_REFRESH_TOKEN";
    public const string EmailNotVerified = "EMAIL_NOT_VERIFIED";
    public const string AccountNotActive = "ACCOUNT_NOT_ACTIVE";
    public const string InvalidAccountRole = "INVALID_ACCOUNT_ROLE";
    public const string AccountNotFound = "ACCOUNT_NOT_FOUND";
    public const string CurrentPasswordInvalid = "CURRENT_PASSWORD_INVALID";
}
