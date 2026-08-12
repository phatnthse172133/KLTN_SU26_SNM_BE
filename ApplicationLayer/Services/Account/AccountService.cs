using AutoMapper;
using System.ComponentModel.DataAnnotations;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using DomainLayer.InterfaceCore.JWT;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Storage;

using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Net;

namespace ApplicationLayer.Services.Account;

public class AccountService : IAccountService
{
    private const string BoothOwnerInvitationType = "BoothOwnerInvitation";
    private const string MarketOwnerInvitationType = "MarketOwnerInvitation";
    private readonly IUserRepository _users;
    private readonly IGenericRepository<Role> _roles;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;
    private readonly IGenericRepository<UserStatusHistory> _history;
    private readonly IGenericRepository<EmailOutbox> _outbox;
    private readonly ILogger<AccountService> _logger;
    private readonly IFileStorageService _fileStorage;
    private readonly IBoothRepository? _booths;
    private readonly IPasswordHasher? _passwordHasher;

    public AccountService(IUserRepository users, IGenericRepository<Role> roles, IMapper mapper, INotificationService notifications, IGenericRepository<UserStatusHistory> history, IGenericRepository<EmailOutbox> outbox, ILogger<AccountService> logger, IFileStorageService fileStorage, IBoothRepository? booths = null, IPasswordHasher? passwordHasher = null)
    {
        _users = users;
        _roles = roles;
        _mapper = mapper;
        _notifications = notifications;
        _history = history;
        _outbox = outbox;
        _logger = logger;
        _fileStorage = fileStorage;
        _booths = booths;
        _passwordHasher = passwordHasher;
    }

    public async Task<ApiResponse<UserResponse>> GetMyAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        return user is null ? throw AppException.NotFound("Account was not found.") : await ToMyAccountResponseAsync(user);
    }

    public async Task<ApiResponse<BoothOwnerAccountInvitationResponse>> CreateBoothOwnerAccountAsync(
        Guid marketOwnerId,
        CreateBoothOwnerAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var email = NormalizeInvitationEmail(request.Email, "Booth Owner", "BOOTH_OWNER");
        // Email addresses are normalized on new writes, but compare case-insensitively
        // as well so legacy records such as Owner@Example.com cannot be duplicated.
        var existing = await _users.FirstOrDefaultAsync(user => user.Email.ToLower() == email);
        if (existing is not null)
        {
            throw AppException.Conflict(
                existing.MustChangePassword && existing.Status == UserStatus.Active
                    ? "A Booth Owner invitation is already pending for this email. Use Resend invitation instead."
                    : "This email is already registered to an account.",
                "BOOTH_OWNER_EMAIL_ALREADY_REGISTERED");
        }

        var role = await _roles.FirstOrDefaultAsync(item => item.RoleName == "BoothOwner");
        if (role is null)
            throw AppException.ServiceUnavailable("Booth Owner role is not configured yet. Please contact Support.", "BOOTH_OWNER_ROLE_UNAVAILABLE");

        var temporaryPassword = GenerateTemporaryPassword();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            UserName = BuildInvitationUserName("booth", Guid.NewGuid()),
            FullName = BuildDisplayName(email),
            Email = email,
            PasswordHash = HashTemporaryPassword(temporaryPassword),
            MustChangePassword = true,
            CreatedByMarketOwnerId = marketOwnerId,
            AuthProvider = AuthProvider.Local,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _users.BeginTransactionAsync();
        try
        {
            await _users.AddAsync(user);
            await _outbox.AddAsync(BuildInvitationEmail(user, temporaryPassword, now, "Booth Owner", BoothOwnerInvitationType));
            await _users.SaveChangesAsync();
            await _users.CommitTransactionAsync();
        }
        catch
        {
            await _users.RollbackTransactionAsync();
            throw;
        }

        _logger.LogInformation(
            "Market owner {MarketOwnerId} created Booth Owner account {BoothOwnerId} and queued its invitation email.",
            marketOwnerId,
            user.Id);

        return ApiResponse<BoothOwnerAccountInvitationResponse>.SuccessResponse(
            ToInvitationResponse(user, invitationStatus: "Pending"),
            "Booth Owner account created. The invitation email is queued for delivery.");
    }

    public async Task<ApiResponse<MarketOwnerAccountInvitationResponse>> CreateMarketOwnerAccountAsync(
        Guid adminId,
        CreateMarketOwnerAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var email = NormalizeInvitationEmail(request.Email, "Market Owner", "MARKET_OWNER");
        var existing = await _users.FirstOrDefaultAsync(user => user.Email.ToLower() == email);
        if (existing is not null)
        {
            throw AppException.Conflict(
                existing.MustChangePassword && existing.Status == UserStatus.Active
                    ? "A Market Owner invitation is already pending for this email. Use Resend invitation instead."
                    : "This email is already registered to an account.",
                "MARKET_OWNER_EMAIL_ALREADY_REGISTERED");
        }

        var role = await _roles.FirstOrDefaultAsync(item => item.RoleName == "MarketOwner");
        if (role is null)
            throw AppException.ServiceUnavailable("Market Owner role is not configured yet. Please contact Support.", "MARKET_OWNER_ROLE_UNAVAILABLE");

        var temporaryPassword = GenerateTemporaryPassword();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            UserName = BuildInvitationUserName("market", Guid.NewGuid()),
            FullName = BuildDisplayName(email, "Market Owner"),
            Email = email,
            PasswordHash = HashTemporaryPassword(temporaryPassword),
            MustChangePassword = true,
            AuthProvider = AuthProvider.Local,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _users.BeginTransactionAsync();
        try
        {
            await _users.AddAsync(user);
            await _outbox.AddAsync(BuildInvitationEmail(user, temporaryPassword, now, "Market Owner", MarketOwnerInvitationType));
            await _users.SaveChangesAsync();
            await _users.CommitTransactionAsync();
        }
        catch
        {
            await _users.RollbackTransactionAsync();
            throw;
        }

        _logger.LogInformation(
            "Admin {AdminId} created Market Owner account {MarketOwnerId} and queued its invitation email.",
            adminId,
            user.Id);

        return ApiResponse<MarketOwnerAccountInvitationResponse>.SuccessResponse(
            ToMarketOwnerInvitationResponse(user, "Pending"),
            "Market Owner account created. The invitation email is queued for delivery.");
    }

    public async Task<ApiResponse<PaginationResp<BoothOwnerAccountInvitationResponse>>> GetCreatedBoothOwnerAccountsAsync(
        Guid marketOwnerId, BoothOwnerAccountListRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accounts = (await _users.FindAsync(user => user.CreatedByMarketOwnerId == marketOwnerId))
            .OrderByDescending(user => user.CreatedAt)
            .ToList();

        var accountIds = accounts.Select(user => user.Id).ToList();
        var invitations = (await _outbox.FindAsync(item =>
                accountIds.Contains(item.ReferenceId) && item.EmailType == BoothOwnerInvitationType))
            .ToDictionary(item => item.ReferenceId);

        var normalizedKeyword = request.Keyword?.Trim();
        var normalizedStatus = request.InvitationStatus?.Trim();
        var response = accounts
            .Select(user => invitations.TryGetValue(user.Id, out var invitation)
                ? ToInvitationResponse(user, invitation.Status, invitation.SentAt)
                : ToInvitationResponse(user, "NotQueued"))
            .Where(item => string.IsNullOrWhiteSpace(normalizedKeyword)
                || item.FullName.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                || item.Email.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                || item.UserName.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(normalizedStatus)
                || string.Equals(item.InvitationStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var pageItems = response
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return ApiResponse<PaginationResp<BoothOwnerAccountInvitationResponse>>.SuccessResponse(
            PaginationResp<BoothOwnerAccountInvitationResponse>.Create(pageItems, response.Count, request));
    }

    public async Task<ApiResponse<BoothOwnerAccountInvitationResponse>> ResendBoothOwnerInvitationAsync(
        Guid marketOwnerId,
        Guid boothOwnerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _users.GetByIdAsync(boothOwnerId);
        if (user is null)
            throw AppException.NotFound("The Booth Owner account could not be found.", "BOOTH_OWNER_ACCOUNT_NOT_FOUND");

        var role = await _roles.GetByIdAsync(user.RoleId);
        if (role is null || !string.Equals(role.RoleName, "BoothOwner", StringComparison.Ordinal))
            throw AppException.BadRequest("The selected account is not a Booth Owner account.", "BOOTH_OWNER_ROLE_REQUIRED");
        if (user.CreatedByMarketOwnerId != marketOwnerId)
            throw AppException.Forbidden(
                "You can resend invitations only for Booth Owner accounts created by your account.",
                "BOOTH_OWNER_INVITATION_NOT_OWNED");
        if (user.Status != UserStatus.Active)
            throw AppException.BadRequest("Only active Booth Owner accounts can receive an invitation.", "BOOTH_OWNER_ACCOUNT_INACTIVE");

        var temporaryPassword = GenerateTemporaryPassword();
        var now = DateTime.UtcNow;
        user.PasswordHash = HashTemporaryPassword(temporaryPassword);
        user.MustChangePassword = true;
        user.UpdatedAt = now;

        var invitation = await _outbox.FirstOrDefaultAsync(item =>
            item.ReferenceId == user.Id && item.EmailType == BoothOwnerInvitationType);
        if (invitation is null)
        {
            invitation = BuildInvitationEmail(user, temporaryPassword, now, "Booth Owner", BoothOwnerInvitationType);
            await _outbox.AddAsync(invitation);
        }
        else
        {
            var replacement = BuildInvitationEmail(user, temporaryPassword, now, "Booth Owner", BoothOwnerInvitationType);
            invitation.RecipientEmail = replacement.RecipientEmail;
            invitation.Subject = replacement.Subject;
            invitation.HtmlBody = replacement.HtmlBody;
            invitation.Status = "Pending";
            invitation.RetryCount = 0;
            invitation.LastError = null;
            invitation.NextRetryAt = null;
            invitation.SentAt = null;
            invitation.UpdatedAt = now;
        }

        _users.Update(user);
        await _users.BeginTransactionAsync();
        try
        {
            await _users.SaveChangesAsync();
            await _users.CommitTransactionAsync();
        }
        catch
        {
            await _users.RollbackTransactionAsync();
            throw;
        }

        _logger.LogInformation(
            "Market owner {MarketOwnerId} requeued a Booth Owner invitation for account {BoothOwnerId}.",
            marketOwnerId,
            user.Id);

        return ApiResponse<BoothOwnerAccountInvitationResponse>.SuccessResponse(
            ToInvitationResponse(user, invitation.Status, invitation.SentAt),
            "A new invitation email is queued for delivery.");
    }

    public async Task<ApiResponse<MarketOwnerAccountInvitationResponse>> ResendMarketOwnerInvitationAsync(
        Guid adminId,
        Guid marketOwnerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _users.GetByIdAsync(marketOwnerId)
            ?? throw AppException.NotFound("The Market Owner account could not be found.", "MARKET_OWNER_ACCOUNT_NOT_FOUND");
        var role = await _roles.GetByIdAsync(user.RoleId);
        if (!string.Equals(role?.RoleName, "MarketOwner", StringComparison.Ordinal))
            throw AppException.BadRequest("The selected account is not a Market Owner account.", "MARKET_OWNER_ROLE_REQUIRED");
        if (user.Status != UserStatus.Active)
            throw AppException.BadRequest("Only active Market Owner accounts can receive an invitation.", "MARKET_OWNER_ACCOUNT_INACTIVE");
        if (!user.MustChangePassword)
            throw AppException.Conflict(
                "This Market Owner has already completed the first sign-in. Use the password reset flow instead.",
                "MARKET_OWNER_INVITATION_ALREADY_ACCEPTED");

        var temporaryPassword = GenerateTemporaryPassword();
        var now = DateTime.UtcNow;
        user.PasswordHash = HashTemporaryPassword(temporaryPassword);
        user.UpdatedAt = now;

        var invitation = await _outbox.FirstOrDefaultAsync(item =>
            item.ReferenceId == user.Id && item.EmailType == MarketOwnerInvitationType);
        var replacement = BuildInvitationEmail(user, temporaryPassword, now, "Market Owner", MarketOwnerInvitationType);
        if (invitation is null)
        {
            invitation = replacement;
            await _outbox.AddAsync(invitation);
        }
        else
        {
            invitation.RecipientEmail = replacement.RecipientEmail;
            invitation.Subject = replacement.Subject;
            invitation.HtmlBody = replacement.HtmlBody;
            invitation.Status = "Pending";
            invitation.RetryCount = 0;
            invitation.LastError = null;
            invitation.NextRetryAt = null;
            invitation.SentAt = null;
            invitation.UpdatedAt = now;
        }

        _users.Update(user);
        await _users.BeginTransactionAsync();
        try
        {
            await _users.SaveChangesAsync();
            await _users.CommitTransactionAsync();
        }
        catch
        {
            await _users.RollbackTransactionAsync();
            throw;
        }

        _logger.LogInformation(
            "Admin {AdminId} requeued a Market Owner invitation for account {MarketOwnerId}.",
            adminId,
            user.Id);

        return ApiResponse<MarketOwnerAccountInvitationResponse>.SuccessResponse(
            ToMarketOwnerInvitationResponse(user, invitation.Status, invitation.SentAt),
            "A new Market Owner invitation email is queued for delivery.");
    }

    private static string NormalizeInvitationEmail(string? value, string roleLabel, string errorPrefix)
    {
        var email = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            throw AppException.BadRequest($"{roleLabel} email is required.", $"{errorPrefix}_EMAIL_REQUIRED");
        return email;
    }

    private static string BuildDisplayName(string email, string fallback = "Booth Owner")
    {
        var localPart = email.Split('@')[0];
        var name = localPart.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private static string BuildInvitationUserName(string prefix, Guid nonce)
        => $"{prefix}_{nonce:N}"[..Math.Min(19, prefix.Length + 33)];

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        var chars = new[]
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)]
        };
        const string all = upper + lower + digits;
        var password = chars.Concat(Enumerable.Range(0, 5).Select(_ => all[RandomNumberGenerator.GetInt32(all.Length)])).ToArray();
        for (var i = password.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }
        return new string(password);
    }

    private string HashTemporaryPassword(string password)
        => (_passwordHasher ?? throw new InvalidOperationException("Password hashing service is not configured.")).HashPassword(password);

    private static EmailOutbox BuildInvitationEmail(
        User user,
        string temporaryPassword,
        DateTime now,
        string roleLabel,
        string emailType)
    {
        var name = WebUtility.HtmlEncode(user.FullName);
        var email = WebUtility.HtmlEncode(user.Email);
        var username = WebUtility.HtmlEncode(user.UserName);
        var password = WebUtility.HtmlEncode(temporaryPassword);
        return new EmailOutbox
        {
            Id = Guid.NewGuid(),
            RecipientEmail = user.Email,
            Subject = $"Your Smart Night Market {roleLabel} account",
            EmailType = emailType,
            ReferenceId = user.Id,
            HtmlBody = $"<div style='font-family:Arial,sans-serif;line-height:1.6'><h2>Welcome to Smart Night Market</h2><p>Hello {name},</p><p>A {roleLabel} account has been created for you by Smart Night Market administration.</p><p><strong>Username:</strong> {username}<br/><strong>Email:</strong> {email}</p><p style='background:#fff3cd;padding:12px;border-left:4px solid #f0ad4e'><strong>Temporary password: {password}</strong></p><p style='color:#b42318'><strong>For your security, sign in and change this temporary password immediately.</strong></p><p>After changing your password, you can complete your profile and access your {roleLabel} workspace.</p><p>Smart Night Market</p></div>",
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static MarketOwnerAccountInvitationResponse ToMarketOwnerInvitationResponse(
        User user,
        string invitationStatus,
        DateTime? invitationSentAt = null)
        => new()
        {
            UserId = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Status = user.Status.ToString(),
            MustChangePassword = user.MustChangePassword,
            InvitationQueued = invitationStatus is "Pending" or "Processing" or "Failed",
            InvitationStatus = invitationStatus,
            InvitationSentAt = invitationSentAt,
            CreatedAt = user.CreatedAt
        };

    private static BoothOwnerAccountInvitationResponse ToInvitationResponse(
        User user,
        string invitationStatus,
        DateTime? invitationSentAt = null)
        => new()
        {
            UserId = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Status = user.Status.ToString(),
            MustChangePassword = user.MustChangePassword,
            InvitationQueued = invitationStatus is "Pending" or "Processing" or "Failed",
            InvitationStatus = invitationStatus,
            InvitationSentAt = invitationSentAt,
            CreatedAt = user.CreatedAt
        };

    public async Task<ApiResponse<UserResponse>> UpdateMyAccountAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw AppException.NotFound("Account was not found.");

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            throw AppException.BadRequest("Full name is required.", "FULL_NAME_REQUIRED");
        if (fullName.Length > 150)
            throw AppException.BadRequest("Full name must not exceed 150 characters.", "FULL_NAME_TOO_LONG");

        var phone = NormalizeVietnamesePhone(request.Phone);

        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        if (address?.Length > 255)
            throw AppException.BadRequest("Address must not exceed 255 characters.", "ADDRESS_TOO_LONG");

        if (request.DoB is { } dateOfBirth && dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw AppException.BadRequest("Date of birth cannot be in the future.", "DATE_OF_BIRTH_INVALID");

        // Explicit allow-list: security/account-owned fields can never be mass-assigned.
        user.FullName = fullName;
        user.Phone = phone;
        user.Address = address;
        user.DoB = request.DoB;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.SaveChangesAsync();
        return await ToMyAccountResponseAsync(user, "Account updated successfully.");
    }

    public async Task<ApiResponse<UserResponse>> UpdateAvatarAsync(
        Guid userId,
        Stream stream,
        string fileName,
        string contentType,
        long length,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw AppException.NotFound("Account was not found.");

        string newAvatarUrl;
        try
        {
            newAvatarUrl = await _fileStorage.SaveAvatarAsync(
                stream, fileName, contentType, length, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Avatar storage failed for user {UserId}.", userId);
            throw AppException.ServiceUnavailable(
                "Avatar upload is temporarily unavailable.",
                "AVATAR_STORAGE_UNAVAILABLE",
                exception);
        }

        var oldAvatarUrl = user.AvatarUrl;
        user.AvatarUrl = newAvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _users.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            await TryDeleteAvatarAsync(newAvatarUrl, userId, "newly uploaded");
            _logger.LogError(exception, "Avatar database update failed for user {UserId}.", userId);
            throw AppException.ServiceUnavailable(
                "Avatar update is temporarily unavailable.",
                "AVATAR_UPDATE_FAILED",
                exception);
        }

        await TryDeleteAvatarAsync(oldAvatarUrl, userId, "replaced");
        return await ToMyAccountResponseAsync(user, "Avatar updated successfully.");
    }

    public async Task<ApiResponse<UserResponse>> RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw AppException.NotFound("Account was not found.");
        var oldAvatarUrl = user.AvatarUrl;
        if (oldAvatarUrl is null) return await ToMyAccountResponseAsync(user, "Avatar is already empty.");

        user.AvatarUrl = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.SaveChangesAsync();
        await TryDeleteAvatarAsync(oldAvatarUrl, userId, "removed");
        return await ToMyAccountResponseAsync(user, "Avatar removed successfully.");
    }

    private async Task TryDeleteAvatarAsync(string? avatarUrl, Guid userId, string kind)
    {
        try
        {
            await _fileStorage.DeleteAvatarIfManagedAsync(avatarUrl);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to delete {AvatarKind} managed avatar for user {UserId}.",
                kind,
                userId);
        }
    }

    private static string? NormalizeVietnamesePhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var raw = input.Trim();
        if (raw.Any(character => !char.IsDigit(character) && character is not '+' and not ' ' and not '.' and not '-' and not '(' and not ')'))
            throw AppException.BadRequest("Phone number format is invalid.", "PHONE_INVALID");

        var compact = new string(raw.Where(character => char.IsDigit(character) || character == '+').ToArray());
        if (compact.StartsWith("+84", StringComparison.Ordinal)) compact = $"0{compact[3..]}";
        else if (compact.StartsWith("84", StringComparison.Ordinal)) compact = $"0{compact[2..]}";

        var validPrefix = compact.StartsWith("03", StringComparison.Ordinal)
            || compact.StartsWith("05", StringComparison.Ordinal)
            || compact.StartsWith("07", StringComparison.Ordinal)
            || compact.StartsWith("08", StringComparison.Ordinal)
            || compact.StartsWith("09", StringComparison.Ordinal);
        if (compact.Length != 10 || compact.Any(character => !char.IsDigit(character)) || !validPrefix || compact.Distinct().Count() == 1)
            throw AppException.BadRequest("Phone number format is invalid.", "PHONE_INVALID");
        return compact;
    }

    public async Task<ApiResponse<PaginationResp<ManagedUserResponse>>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        // Build filter predicate
        System.Linq.Expressions.Expression<Func<User, bool>>? filter = null;

        // We need to build a composite filter
        var filters = new List<System.Linq.Expressions.Expression<Func<User, bool>>>();

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleEntity = (await _roles.FindAsync(r => r.RoleName == query.Role)).FirstOrDefault();
            if (roleEntity != null)
            {
                var roleId = roleEntity.Id;
                filters.Add(u => u.RoleId == roleId);
            }
            else
            {
                // Role doesn't exist, return empty
                return ApiResponse<PaginationResp<ManagedUserResponse>>.SuccessResponse(
                    PaginationResp<ManagedUserResponse>.Create(new List<ManagedUserResponse>(), 0, query));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<UserStatus>(query.Status, true, out var statusEnum))
            {
                filters.Add(u => u.Status == statusEnum);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            filters.Add(u => u.UserName.ToLower().Contains(keyword) ||
                             u.FullName.ToLower().Contains(keyword) ||
                             u.Email.ToLower().Contains(keyword));
        }

        // Combine filters
        if (filters.Count > 0)
        {
            filter = filters[0];
            for (int i = 1; i < filters.Count; i++)
            {
                var left = filter;
                var right = filters[i];
                var param = System.Linq.Expressions.Expression.Parameter(typeof(User), "u");
                var body = System.Linq.Expressions.Expression.AndAlso(
                    System.Linq.Expressions.Expression.Invoke(left, param),
                    System.Linq.Expressions.Expression.Invoke(right, param));
                filter = System.Linq.Expressions.Expression.Lambda<Func<User, bool>>(body, param);
            }
        }

        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var page = await _users.GetPagedAsync(
            filter, query.Page, query.PageSize, u => u.CreatedAt, descending, cancellationToken);

        // Batch load roles to avoid N+1
        var roleIds = page.Items.Select(u => u.RoleId).Distinct().ToList();
        var roles = await _roles.FindAsync(r => roleIds.Contains(r.Id));
        var roleDict = roles.ToDictionary(r => r.Id, r => r.RoleName);

        var responses = page.Items.Select(user =>
        {
            var response = _mapper.Map<ManagedUserResponse>(user);
            response.Role = roleDict.TryGetValue(user.RoleId, out var roleName) ? roleName : "Unknown";
            return response;
        }).ToList();

        return ApiResponse<PaginationResp<ManagedUserResponse>>.SuccessResponse(
            PaginationResp<ManagedUserResponse>.Create(responses, page.TotalCount, query));
    }

    public async Task<ApiResponse<ManagedUserResponse>> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        return user is null ? throw AppException.NotFound("Account was not found.")
            : ApiResponse<ManagedUserResponse>.SuccessResponse(await ToManagedUserResponseAsync(user));
    }

    public async Task<ApiResponse<BoothOwnerAccountDetailResponse>> GetBoothOwnerDetailsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw AppException.NotFound("This account no longer exists.", "ACCOUNT_NOT_FOUND");
        var role = await _roles.GetByIdAsync(user.RoleId);
        if (!string.Equals(role?.RoleName, "BoothOwner", StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("This account is not a Booth Owner account.", "USER_NOT_BOOTH_OWNER");
        if (_booths is null)
            throw AppException.ServiceUnavailable(
                "Booth account details are temporarily unavailable.",
                "BOOTH_DETAILS_UNAVAILABLE");

        var booth = await _booths.GetByOwnerIdWithAdminDetailsAsync(userId, cancellationToken);
        var response = new BoothOwnerAccountDetailResponse
        {
            Account = await ToManagedUserResponseAsync(user)
        };

        if (booth is null)
            return ApiResponse<BoothOwnerAccountDetailResponse>.SuccessResponse(response);

        var activeLocation = booth.BoothLocations
            .Where(location => !location.IsDeleted && location.ReleasedAt == null)
            .OrderByDescending(location => location.UpdatedAt)
            .FirstOrDefault();
        var activeSubscription = booth.BoothSubscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.EndDate)
            .FirstOrDefault();
        var documents = booth.BoothDocuments
            .OrderByDescending(document => document.CreatedAt)
            .Select(document => new BoothDocumentResponse
            {
                Id = document.Id,
                DocumentType = document.DocumentType.ToString(),
                FileUrl = document.FileUrl,
                VerificationStatus = document.VerificationStatus.ToString(),
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt
            })
            .ToList();

        response.OwnedBooths.Add(new AdminOwnedBoothResponse
        {
            Id = booth.Id,
            BoothName = booth.BoothName,
            BoothCode = booth.BoothCode,
            Status = booth.Status.ToString(),
            Description = booth.Description,
            PhoneNumber = booth.PhoneNumber,
            ThumbnailUrl = booth.ThumbnailUrl,
            LogoUrl = booth.ThumbnailUrl,
            NightMarketName = booth.NightMarket?.Name,
            ZoneName = activeLocation?.Zone?.ZoneName ?? booth.Zone?.ZoneName,
            SlotNumber = activeLocation?.SlotNumber ?? booth.SlotNumber,
            MapPositionX = activeLocation?.Xcoordinate ?? booth.MapPositionX,
            MapPositionY = activeLocation?.Ycoordinate ?? booth.MapPositionY,
            ActivePackageName = activeSubscription?.Package?.PackageName,
            PackageExpiryDate = activeSubscription?.EndDate,
            CreatedAt = booth.CreatedAt,
            Documents = documents
        });

        return ApiResponse<BoothOwnerAccountDetailResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<ManagedUserResponse>> ChangeUserStatusAsync(Guid adminId, Guid userId, ChangeUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (adminId == userId)
            throw AppException.BadRequest("Administrators cannot change the status of their own account.");

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw AppException.BadRequest("Reason is required and must be at least 10 characters.");

        if (reason.Length > 1000)
            throw AppException.BadRequest("Reason must not exceed 1000 characters.");

        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw AppException.NotFound("Account was not found.");

        if (request.Status is not UserStatus.Active and not UserStatus.Inactive)
            throw AppException.BadRequest("Account status must be Active or Inactive.");

        if (user.Status == request.Status)
            throw AppException.BadRequest($"Account is already {request.Status}.");

        var validTransition = (user.Status == UserStatus.Active && request.Status == UserStatus.Inactive) ||
                              (user.Status == UserStatus.Inactive && request.Status == UserStatus.Active);
        if (!validTransition)
            throw AppException.BadRequest($"Cannot change account status from {user.Status} to {request.Status}.");

        var previousStatus = user.Status;
        var now = DateTime.UtcNow;

        await _users.BeginTransactionAsync();
        try
        {
            var rowsAffected = await _users.UpdateStatusWithConcurrencyAsync(userId, previousStatus, request.Status, now);
            if (rowsAffected == 0)
            {
                throw AppException.Conflict("Account status has been modified by another administrator.", "USER_STATUS_CONFLICT");
            }

        var history = new UserStatusHistory
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ChangedByAdminId = adminId,
            PreviousStatus = previousStatus,
            NewStatus = request.Status,
            Reason = reason,
            CreatedAt = now
        };

        var emailOutbox = new EmailOutbox
        {
            Id = Guid.NewGuid(),
            RecipientEmail = user.Email,
            Subject = request.Status == UserStatus.Inactive ? "Your Smart Night Market account has been deactivated" : "Your Smart Night Market account has been reactivated",
            HtmlBody = reason,
            EmailType = request.Status == UserStatus.Inactive ? "AccountBanned" : "AccountUnbanned",
            ReferenceId = history.Id,
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };
        // Let's build the HTML body here to be safe
        var fullNameEncoded = System.Net.WebUtility.HtmlEncode(user.FullName);
        var reasonEncoded = System.Net.WebUtility.HtmlEncode(reason);
        if (request.Status == UserStatus.Inactive)
        {
            emailOutbox.HtmlBody = $"Hello {fullNameEncoded},<br/><br/>Your Smart Night Market account has been deactivated.<br/><br/>Reason:<br/>{reasonEncoded}<br/><br/>Effective at:<br/>{now:yyyy-MM-dd HH:mm:ss} UTC<br/><br/>You will not be able to sign in or use account features until your account is reactivated.<br/><br/>If you need assistance, please contact the Smart Night Market administration team.";
        }
        else
        {
            emailOutbox.HtmlBody = $"Hello {fullNameEncoded},<br/><br/>Your Smart Night Market account has been reactivated and you can sign in again.<br/><br/>Reason:<br/>{reasonEncoded}<br/><br/>Reactivated at:<br/>{now:yyyy-MM-dd HH:mm:ss} UTC";
        }

        await _history.AddAsync(history);
        await _outbox.AddAsync(emailOutbox);

        await _users.SaveChangesAsync();
        await _users.CommitTransactionAsync();
        }
        catch (Exception)
        {
            await _users.RollbackTransactionAsync();
            throw;
        }

        await _users.ReloadAsync(user);

        var notificationType = request.Status == UserStatus.Inactive ? NotificationType.AccountDeactivated : NotificationType.AccountReactivated;
        var notificationTitle = request.Status == UserStatus.Inactive ? "Account Deactivated" : "Account Reactivated";
        var notificationContent = request.Status == UserStatus.Inactive ? $"Your account has been deactivated.\nReason: {reason}" : $"Your account has been reactivated.\nReason: {reason}";

        try
        {
            await _notifications.NotifyAsync(new NotificationMessage(
                user.Id,
                notificationType,
                notificationTitle,
                notificationContent,
                ReferenceType: "Account",
                ReferenceId: user.Id), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification for account status change to user {UserId}.", user.Id);
        }

        return ApiResponse<ManagedUserResponse>.SuccessResponse(await ToManagedUserResponseAsync(user), "Account status updated successfully.");
    }

    public async Task<ApiResponse<PaginationResp<UserStatusHistoryResponse>>> GetUserStatusHistoryAsync(Guid userId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user == null)
            throw AppException.NotFound("Account was not found.");

        var page = await _history.GetPagedAsync(
            h => h.UserId == userId,
            pagination.Page,
            pagination.PageSize,
            h => h.CreatedAt,
            false,
            cancellationToken);

        var adminIds = page.Items.Select(h => h.ChangedByAdminId).Distinct().ToList();
        var admins = await _users.FindAsync(u => adminIds.Contains(u.Id));
        var adminDict = admins.ToDictionary(a => a.Id);

        var responses = new List<UserStatusHistoryResponse>();
        foreach (var h in page.Items)
        {
            var adminName = adminDict.TryGetValue(h.ChangedByAdminId, out var admin) ? admin.FullName : "Unknown Admin";
            responses.Add(new UserStatusHistoryResponse
            {
                Id = h.Id,
                UserId = h.UserId,
                ChangedByAdminId = h.ChangedByAdminId,
                ChangedByAdminName = adminName,
                PreviousStatus = h.PreviousStatus.ToString(),
                NewStatus = h.NewStatus.ToString(),
                Reason = h.Reason,
                CreatedAt = h.CreatedAt
            });
        }

        return ApiResponse<PaginationResp<UserStatusHistoryResponse>>.SuccessResponse(
            PaginationResp<UserStatusHistoryResponse>.Create(responses, page.TotalCount, pagination));
    }

    private async Task<ApiResponse<UserResponse>> ToMyAccountResponseAsync(User user, string message = "Success")
    {
        var role = await _roles.GetByIdAsync(user.RoleId);
        if (role is null)
            throw AppException.BadRequest("Account has no valid assigned role.");

        var response = _mapper.Map<UserResponse>(user);
        response.Role = role.RoleName;
        return ApiResponse<UserResponse>.SuccessResponse(response, message);
    }

    private async Task<ManagedUserResponse> ToManagedUserResponseAsync(User user)
    {
        var response = _mapper.Map<ManagedUserResponse>(user);
        response.Role = (await _roles.GetByIdAsync(user.RoleId))?.RoleName ?? "Unknown";
        return response;
    }
}
