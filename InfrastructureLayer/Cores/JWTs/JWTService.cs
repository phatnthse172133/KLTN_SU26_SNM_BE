using DomainLayer.InterfaceCore.JWT;
using InfrastructureLayer.Cores.Helppers;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace InfrastructureLayer.Cores.JWTs;

public class JWTService : IJwtService
{
    private readonly JwtSettings _settings;

    public JWTService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public string GenerateAccessToken(Guid userId, string email, string role)
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey) || _settings.SecretKey.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be configured with at least 32 characters.");

        var payload = new InfrastructureLayer.Cores.Helppers.JwtPayload(userId, email, role, Guid.NewGuid().ToString("N"));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, payload.UserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, payload.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, payload.Email),
            new Claim(ClaimTypes.Email, payload.Email),
            new Claim(ClaimTypes.Role, payload.Role),
            new Claim(JwtRegisteredClaimNames.Jti, payload.TokenId)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var token = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateSecureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public string GenerateNumericCode(int digits)
    {
        if (digits is < 1 or > 9)
            throw new ArgumentOutOfRangeException(nameof(digits), "Code length must be between 1 and 9 digits.");

        var upperBound = (int)Math.Pow(10, digits);
        return RandomNumberGenerator.GetInt32(upperBound).ToString($"D{digits}");
    }

    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public DateTime GetAccessTokenExpiry() => DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
    public DateTime GetRefreshTokenExpiry() => DateTime.UtcNow.AddDays(_settings.RefreshTokenDays);
    public DateTime GetEmailVerificationExpiry() => DateTime.UtcNow.AddHours(_settings.EmailVerificationHours);
}
