namespace DomainLayer.InterfaceCore.Auth;

/// <summary>Temporary authentication state backed by a TTL-capable cache.</summary>
public interface IAuthTokenStore
{
    Task StoreRefreshTokenAsync(string tokenHash, Guid userId, TimeSpan ttl);
    Task<Guid?> ConsumeRefreshTokenAsync(string tokenHash);
    Task RevokeRefreshTokenAsync(string tokenHash);
    Task RevokeAllRefreshTokensAsync(Guid userId);

    Task StoreEmailVerificationAsync(string tokenHash, Guid userId, TimeSpan ttl);
    Task<Guid?> ConsumeEmailVerificationAsync(string tokenHash);

    Task StorePasswordResetOtpAsync(Guid userId, string otpHash, TimeSpan ttl);
    Task<bool> IsPasswordResetOtpValidAsync(Guid userId, string otpHash);
    Task<bool> ConsumePasswordResetOtpAsync(Guid userId, string otpHash);
    Task StorePasswordResetTokenAsync(string tokenHash, Guid userId, TimeSpan ttl);
    Task<Guid?> ConsumePasswordResetTokenAsync(string tokenHash);
}
