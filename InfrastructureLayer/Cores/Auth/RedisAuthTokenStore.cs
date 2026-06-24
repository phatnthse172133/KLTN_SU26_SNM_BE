using DomainLayer.InterfaceCore.Auth;
using StackExchange.Redis;

namespace InfrastructureLayer.Cores.Auth;

public class RedisAuthTokenStore : IAuthTokenStore
{
    private readonly IDatabase _database;

    public RedisAuthTokenStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task StoreRefreshTokenAsync(string tokenHash, Guid userId, TimeSpan ttl)
    {
        await _database.StringSetAsync(RefreshKey(tokenHash), userId.ToString(), ttl);
        await _database.SetAddAsync(UserRefreshTokensKey(userId), tokenHash);
        await _database.KeyExpireAsync(UserRefreshTokensKey(userId), ttl);
    }

    public async Task<Guid?> ConsumeRefreshTokenAsync(string tokenHash)
    {
        var value = await _database.StringGetDeleteAsync(RefreshKey(tokenHash));
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    public Task RevokeRefreshTokenAsync(string tokenHash) => _database.KeyDeleteAsync(RefreshKey(tokenHash));

    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        var setKey = UserRefreshTokensKey(userId);
        var hashes = await _database.SetMembersAsync(setKey);
        if (hashes.Length > 0)
            await _database.KeyDeleteAsync(hashes.Select(hash => (RedisKey)RefreshKey(hash!)).ToArray());
        await _database.KeyDeleteAsync(setKey);
    }

    public Task StoreEmailVerificationAsync(string tokenHash, Guid userId, TimeSpan ttl) =>
        _database.StringSetAsync(EmailVerificationKey(tokenHash), userId.ToString(), ttl);

    public async Task<Guid?> ConsumeEmailVerificationAsync(string tokenHash)
    {
        var value = await _database.StringGetDeleteAsync(EmailVerificationKey(tokenHash));
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    public Task StorePasswordResetOtpAsync(Guid userId, string otpHash, TimeSpan ttl) =>
        _database.StringSetAsync(PasswordResetOtpKey(userId), otpHash, ttl);

    public async Task<bool> IsPasswordResetOtpValidAsync(Guid userId, string otpHash)
    {
        var value = await _database.StringGetAsync(PasswordResetOtpKey(userId));
        return value.HasValue && value == otpHash;
    }

    public async Task<bool> ConsumePasswordResetOtpAsync(Guid userId, string otpHash)
    {
        const string script = "local value = redis.call('GET', KEYS[1]); if value == ARGV[1] then return redis.call('DEL', KEYS[1]); end return 0;";
        var result = (int)await _database.ScriptEvaluateAsync(script, new RedisKey[] { PasswordResetOtpKey(userId) }, new RedisValue[] { otpHash });
        return result == 1;
    }

    public Task StorePasswordResetTokenAsync(string tokenHash, Guid userId, TimeSpan ttl) =>
        _database.StringSetAsync(PasswordResetTokenKey(tokenHash), userId.ToString(), ttl);

    public async Task<Guid?> ConsumePasswordResetTokenAsync(string tokenHash)
    {
        var value = await _database.StringGetDeleteAsync(PasswordResetTokenKey(tokenHash));
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static RedisKey RefreshKey(string hash) => $"auth:refresh:{hash}";
    private static RedisKey UserRefreshTokensKey(Guid userId) => $"auth:refresh:user:{userId:N}";
    private static RedisKey EmailVerificationKey(string hash) => $"auth:email-verification:{hash}";
    private static RedisKey PasswordResetOtpKey(Guid userId) => $"auth:password-reset-otp:{userId:N}";
    private static RedisKey PasswordResetTokenKey(string hash) => $"auth:password-reset-token:{hash}";
}
