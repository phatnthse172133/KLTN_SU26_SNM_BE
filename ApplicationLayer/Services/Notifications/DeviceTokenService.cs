using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Notifications;

public class DeviceTokenService : IDeviceTokenService
{
    private readonly IUserDeviceTokenRepository _tokens;
    private readonly IUserRepository _users;
    private readonly IMapper _mapper;

    public DeviceTokenService(IUserDeviceTokenRepository tokens, IUserRepository users, IMapper mapper)
    {
        _tokens = tokens;
        _users = users;
        _mapper = mapper;
    }

    public async Task<ApiResponse<DeviceTokenResponse>> RegisterAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (await _users.GetByIdAsync(userId) is null)
            throw AppException.NotFound("User was not found.", "USER_NOT_FOUND");

        var normalizedToken = request.Token.Trim();
        if (normalizedToken.Length < 10)
            throw AppException.BadRequest("Device token is invalid.", "DEVICE_TOKEN_INVALID");

        var now = DateTime.UtcNow;
        var token = await _tokens.GetByTokenIncludingDeletedAsync(normalizedToken, cancellationToken);
        if (token is null)
        {
            token = new UserDeviceToken
            {
                Id = Guid.NewGuid(),
                Token = normalizedToken,
                CreatedAt = now
            };
            await _tokens.AddAsync(token);
        }

        token.UserId = userId;
        token.Platform = request.Platform;
        token.DeviceId = string.IsNullOrWhiteSpace(request.DeviceId)
            ? null
            : request.DeviceId.Trim();
        token.IsActive = true;
        token.IsDeleted = false;
        token.LastUsedAt = now;
        token.UpdatedAt = now;
        await _tokens.SaveChangesAsync();

        return ApiResponse<DeviceTokenResponse>.SuccessResponse(
            _mapper.Map<DeviceTokenResponse>(token),
            "Device token registered successfully.");
    }

    public async Task RemoveAsync(Guid userId, string? token, string? deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(deviceId))
        {
            throw AppException.BadRequest(
                "Token or deviceId is required.",
                "DEVICE_TOKEN_INVALID");
        }

        var matches = await _tokens.GetByUserAndSelectionAsync(
            userId,
            token?.Trim(),
            deviceId?.Trim(),
            cancellationToken);
        if (matches.Count == 0)
            throw AppException.NotFound("Device token was not found.", "DEVICE_TOKEN_NOT_FOUND");

        var now = DateTime.UtcNow;
        foreach (var item in matches)
        {
            item.IsActive = false;
            item.UpdatedAt = now;
            _tokens.Delete(item);
        }

        await _tokens.SaveChangesAsync();
    }

    public async Task RemoveAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var matches = await _tokens.GetActiveByUserAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var item in matches)
        {
            item.IsActive = false;
            item.UpdatedAt = now;
            _tokens.Delete(item);
        }

        if (matches.Count > 0)
            await _tokens.SaveChangesAsync();
    }
}
