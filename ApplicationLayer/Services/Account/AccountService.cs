using AutoMapper;
using System.ComponentModel.DataAnnotations;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Storage;

using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.Account;

public class AccountService : IAccountService
{
    private readonly IUserRepository _users;
    private readonly IGenericRepository<Role> _roles;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;
    private readonly IGenericRepository<UserStatusHistory> _history;
    private readonly IGenericRepository<EmailOutbox> _outbox;
    private readonly ILogger<AccountService> _logger;
    private readonly IFileStorageService _fileStorage;
    private readonly IBoothRepository? _booths;

    public AccountService(IUserRepository users, IGenericRepository<Role> roles, IMapper mapper, INotificationService notifications, IGenericRepository<UserStatusHistory> history, IGenericRepository<EmailOutbox> outbox, ILogger<AccountService> logger, IFileStorageService fileStorage, IBoothRepository? booths = null)
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
    }

    public async Task<ApiResponse<UserResponse>> GetMyAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId);
        return user is null ? throw AppException.NotFound("Account was not found.") : await ToMyAccountResponseAsync(user);
    }

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
            .Concat(booth.Registration?.BoothDocuments ?? [])
            .GroupBy(document => document.Id)
            .Select(group => group.First())
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
