namespace DomainLayer.InterfaceCore.JWT;

public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string email, string role);
    string GenerateSecureToken();
    string GenerateNumericCode(int digits);
    string HashToken(string token);
    DateTime GetAccessTokenExpiry();
    DateTime GetRefreshTokenExpiry();
    DateTime GetEmailVerificationExpiry();
}
