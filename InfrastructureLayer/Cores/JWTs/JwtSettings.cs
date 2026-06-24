namespace InfrastructureLayer.Cores.JWTs;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "SmartNightMarket";
    public string Audience { get; init; } = "SmartNightMarketClient";
    public string SecretKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 60;
    public int RefreshTokenDays { get; init; } = 14;
    public int EmailVerificationHours { get; init; } = 24;
}
