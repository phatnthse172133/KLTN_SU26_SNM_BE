using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Notifications;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;
using System.Text.Json;

namespace ApplicationLayer.Services.AdminModeration;

public class AdminModerationService : IAdminModerationService
{
    private readonly IModerationRepository _moderationRepo;
    private readonly INightMarketRepository _marketRepo;
    private readonly IBoothRepository _boothRepo;
    private readonly IUserRepository _userRepo;
    private readonly INotificationService _notifications;
    private readonly ILogger<AdminModerationService> _logger;

    public AdminModerationService(
        IModerationRepository moderationRepo,
        INightMarketRepository marketRepo,
        IBoothRepository boothRepo,
        IUserRepository userRepo,
        INotificationService notifications,
        ILogger<AdminModerationService> logger)
    {
        _moderationRepo = moderationRepo;
        _marketRepo = marketRepo;
        _boothRepo = boothRepo;
        _userRepo = userRepo;
        _notifications = notifications;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════
    //  Night Market
    // ════════════════════════════════════════════════════════════

    public async Task<ApiResponse<PaginationResp<MarketModerationOverviewResponse>>> GetMarketsAsync(
        AdminMarketModerationQueryRequest request, CancellationToken cancellationToken = default)
    {
        var page = await _moderationRepo.GetMarketsPagedAsync(
            request.Keyword,
            request.LifecycleStatus,
            request.ModerationStatus,
            request.MarketOwnerId,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        var ownerIds = page.Items
            .Where(m => m.MarketOwnerId.HasValue)
            .Select(m => m.MarketOwnerId!.Value)
            .Distinct()
            .ToList();

        var ownerNames = ownerIds.Count > 0
            ? await _userRepo.GetUserNamesByIdsAsync(ownerIds, cancellationToken)
            : new Dictionary<Guid, string>();

        var marketIds = page.Items.Select(m => m.Id).ToList();
        var boothCounts = await _moderationRepo.CountBoothsByMarketIdsAsync(marketIds, cancellationToken);
        var activeBoothCounts = await _moderationRepo.CountActiveBoothsByMarketIdsAsync(marketIds, cancellationToken);
        var seriousComplaintCounts = await _moderationRepo.CountSeriousComplaintsByMarketIdsAsync(marketIds, cancellationToken);

        var items = page.Items.Select(m => MapMarketOverview(
            m, ownerNames, boothCounts, activeBoothCounts, seriousComplaintCounts)).ToList();

        return ApiResponse<PaginationResp<MarketModerationOverviewResponse>>.SuccessResponse(
            PaginationResp<MarketModerationOverviewResponse>.Create(items, page.TotalCount, request));
    }

    public async Task<ApiResponse<MarketModerationDetailResponse>> GetMarketDetailAsync(
        Guid marketId, CancellationToken cancellationToken = default)
    {
        var market = await _moderationRepo.GetMarketDetailAsync(marketId, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        var totalBooths = await _moderationRepo.CountBoothsByMarketAsync(marketId, cancellationToken);
        var activeBooths = await _moderationRepo.CountActiveBoothsByMarketAsync(marketId, cancellationToken);
        var totalComplaints = await _moderationRepo.CountComplaintsByMarketAsync(marketId, cancellationToken);
        var seriousComplaints = await _moderationRepo.CountSeriousComplaintsByMarketAsync(marketId, cancellationToken);
        var recentComplaints = await _moderationRepo.GetRecentComplaintsByMarketAsync(marketId, 5, cancellationToken);

        var detail = MapMarketDetail(market, totalBooths, activeBooths, totalComplaints, seriousComplaints, recentComplaints);

        return ApiResponse<MarketModerationDetailResponse>.SuccessResponse(detail);
    }

    public async Task<ApiResponse<ModerationActionResponse>> ChangeMarketModerationStatusAsync(
        Guid adminId, string adminName, Guid marketId, ChangeModerationStatusRequest request, CancellationToken cancellationToken = default)
    {
        ValidateReason(request.Reason);
        ValidateModerationStatus(request.Status);

        var market = await _moderationRepo.GetMarketDetailAsync(marketId, cancellationToken);
        if (market is null)
            throw AppException.NotFound("Night market was not found.");

        if (market.ModerationStatus == request.Status)
            throw AppException.BadRequest($"Night market is already {request.Status}.");

        if (request.ExpectedUpdatedAt.HasValue && market.UpdatedAt != request.ExpectedUpdatedAt.Value)
            throw AppException.Conflict("Night market has been modified by another user. Please refresh and try again.", "MODERATION_CONFLICT");

        var now = DateTime.UtcNow;
        var previousStatus = market.ModerationStatus;
        ModerationActionHistory history;

        await _moderationRepo.BeginTransactionAsync();
        try
        {
            var rowsAffected = await _moderationRepo.UpdateMarketModerationStatusAsync(
                marketId, previousStatus, request.Status, now, cancellationToken);

            if (rowsAffected == 0)
                throw AppException.Conflict("Night market has been modified by another user. Please refresh and try again.", "MODERATION_CONFLICT");

            // Save history
            history = new ModerationActionHistory
            {
                Id = Guid.NewGuid(),
                NightMarketId = marketId,
                AdminId = adminId,
                AdminName = adminName,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = request.Status.ToString(),
                Reason = request.Reason.Trim(),
                Source = request.ComplaintId.HasValue ? ModerationActionSource.Complaint : ModerationActionSource.DirectAdmin,
                ComplaintId = request.ComplaintId,
                CreatedAt = now
            };
            await _moderationRepo.AddAsync(history);
            await _moderationRepo.SaveChangesAsync();
            await _moderationRepo.CommitTransactionAsync();
        }
        catch (AppException)
        {
            await _moderationRepo.RollbackTransactionAsync();
            throw;
        }
        catch (Exception)
        {
            await _moderationRepo.RollbackTransactionAsync();
            throw;
        }

        // Notify market owner
        if (market.MarketOwnerId.HasValue)
        {
            try
            {
                var title = request.Status == ModerationStatus.Suspended ? "Night market suspended" : "Night market restored";
                var content = request.Status == ModerationStatus.Suspended
                    ? $"Your night market \"{market.Name}\" has been suspended.\n\nReason: {request.Reason.Trim()}"
                    : $"Your night market \"{market.Name}\" has been restored.\n\nReason: {request.Reason.Trim()}";

                await _notifications.NotifyAsync(new NotificationMessage(
                    market.MarketOwnerId.Value,
                    NotificationType.System,
                    title,
                    content,
                    null,
                    "NightMarket",
                    marketId,
                    JsonSerializer.Serialize(new { marketId, action = request.Status.ToString() })), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify market owner {OwnerId} about moderation change for market {MarketId}", market.MarketOwnerId, marketId);
            }
        }

        return ApiResponse<ModerationActionResponse>.SuccessResponse(
            new ModerationActionResponse { Id = history.Id, Success = true, Message = $"Night market has been {request.Status.ToString().ToLower()}." },
            request.Status == ModerationStatus.Suspended ? "Night market has been suspended." : "Night market has been restored.");
    }

    public async Task<ApiResponse<PaginationResp<ModerationActionHistoryResponse>>> GetMarketHistoryAsync(
        Guid marketId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (await _moderationRepo.GetMarketDetailAsync(marketId, cancellationToken) is null)
            throw AppException.NotFound("Night market was not found.");

        var result = await _moderationRepo.GetHistoryByMarketAsync(marketId, page, pageSize, cancellationToken);
        var items = result.Items.Select(MapHistory).ToList();

        return ApiResponse<PaginationResp<ModerationActionHistoryResponse>>.SuccessResponse(
            PaginationResp<ModerationActionHistoryResponse>.Create(
                items, result.TotalCount, new PaginationReq { Page = page, PageSize = pageSize }));
    }

    // ════════════════════════════════════════════════════════════
    //  Booth
    // ════════════════════════════════════════════════════════════

    public async Task<ApiResponse<PaginationResp<BoothModerationOverviewResponse>>> GetBoothsAsync(
        AdminBoothModerationQueryRequest request, CancellationToken cancellationToken = default)
    {
        var page = await _moderationRepo.GetBoothsPagedAsync(
            request.Keyword,
            request.Status,
            request.NightMarketId,
            request.BoothOwnerId,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase),
            cancellationToken);

        var boothIds = page.Items.Select(b => b.Id).ToList();
        var complaintCounts = await _moderationRepo.CountComplaintsByBoothIdsAsync(boothIds, cancellationToken);

        var items = page.Items.Select(booth =>
            MapBoothOverview(booth, complaintCounts.GetValueOrDefault(booth.Id))).ToList();

        return ApiResponse<PaginationResp<BoothModerationOverviewResponse>>.SuccessResponse(
            PaginationResp<BoothModerationOverviewResponse>.Create(items, page.TotalCount, request));
    }

    public async Task<ApiResponse<BoothModerationDetailResponse>> GetBoothDetailAsync(
        Guid boothId, CancellationToken cancellationToken = default)
    {
        var booth = await _moderationRepo.GetBoothDetailAsync(boothId, cancellationToken);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        var complaintCount = await _moderationRepo.CountComplaintsByBoothAsync(boothId, cancellationToken);
        var recentComplaints = await _moderationRepo.GetRecentComplaintsByBoothAsync(boothId, 5, cancellationToken);

        var detail = MapBoothDetail(booth, complaintCount, recentComplaints);

        return ApiResponse<BoothModerationDetailResponse>.SuccessResponse(detail);
    }

    public async Task<ApiResponse<ModerationActionResponse>> BanBoothAsync(
        Guid adminId, string adminName, Guid boothId, BoothModerationActionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateReason(request.Reason);

        var booth = await _moderationRepo.GetBoothDetailAsync(boothId, cancellationToken);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        if (booth.Status == BoothStatus.Banned)
            throw AppException.BadRequest("Booth is already banned.");

        var history = await ExecuteBoothStatusChangeAsync(adminId, adminName, booth, BoothStatus.Banned, request, cancellationToken);

        await NotifyBoothOwnerAsync(
            booth,
            "Booth banned",
            $"Your booth \"{booth.BoothName}\" has been banned by the platform.\n\nReason: {request.Reason.Trim()}",
            BoothStatus.Banned,
            cancellationToken);

        return ApiResponse<ModerationActionResponse>.SuccessResponse(
            new ModerationActionResponse { Id = history.Id, Success = true, Message = "Booth has been banned." },
            "Booth has been banned.");
    }

    public async Task<ApiResponse<ModerationActionResponse>> RestoreBoothAsync(
        Guid adminId, string adminName, Guid boothId, BoothModerationActionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateReason(request.Reason);

        var booth = await _moderationRepo.GetBoothDetailAsync(boothId, cancellationToken);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        if (booth.Status != BoothStatus.Banned)
            throw AppException.BadRequest("Only a banned booth can be restored.");

        var history = await ExecuteBoothStatusChangeAsync(adminId, adminName, booth, BoothStatus.Active, request, cancellationToken);

        await NotifyBoothOwnerAsync(
            booth,
            "Booth restored",
            $"Your booth \"{booth.BoothName}\" has been restored.\n\nReason: {request.Reason.Trim()}",
            BoothStatus.Active,
            cancellationToken);

        return ApiResponse<ModerationActionResponse>.SuccessResponse(
            new ModerationActionResponse { Id = history.Id, Success = true, Message = "Booth has been restored." },
            "Booth has been restored.");
    }

    private async Task<ModerationActionHistory> ExecuteBoothStatusChangeAsync(
        Guid adminId, string adminName, Booth booth, BoothStatus targetStatus, BoothModerationActionRequest request, CancellationToken cancellationToken)
    {
        if (request.ExpectedUpdatedAt.HasValue && booth.UpdatedAt != request.ExpectedUpdatedAt.Value)
            throw AppException.Conflict("Booth has been modified by another user. Please refresh and try again.", "MODERATION_CONFLICT");

        var now = DateTime.UtcNow;
        var previousStatus = booth.Status;
        ModerationActionHistory history;

        await _moderationRepo.BeginTransactionAsync();
        try
        {
            var rowsAffected = await _moderationRepo.UpdateBoothStatusAsync(
                booth.Id, previousStatus, targetStatus, now, cancellationToken);

            if (rowsAffected == 0)
                throw AppException.Conflict("Booth has been modified by another user. Please refresh and try again.", "MODERATION_CONFLICT");

            history = new ModerationActionHistory
            {
                Id = Guid.NewGuid(),
                BoothId = booth.Id,
                AdminId = adminId,
                AdminName = adminName,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = targetStatus.ToString(),
                Reason = request.Reason.Trim(),
                Source = request.ComplaintId.HasValue ? ModerationActionSource.Complaint : ModerationActionSource.DirectAdmin,
                ComplaintId = request.ComplaintId,
                CreatedAt = now
            };
            await _moderationRepo.AddAsync(history);
            await _moderationRepo.SaveChangesAsync();
            await _moderationRepo.CommitTransactionAsync();
        }
        catch (AppException)
        {
            await _moderationRepo.RollbackTransactionAsync();
            throw;
        }
        catch (Exception)
        {
            await _moderationRepo.RollbackTransactionAsync();
            throw;
        }

        return history;
    }

    private async Task NotifyBoothOwnerAsync(
        Booth booth, string title, string content, BoothStatus action, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.NotifyAsync(new NotificationMessage(
                booth.BoothOwnerId,
                NotificationType.BoothSuspended,
                title,
                content,
                booth.Id,
                "Booth",
                booth.Id,
                JsonSerializer.Serialize(new { boothId = booth.Id, action = action.ToString() })), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify booth owner {OwnerId} about moderation change for booth {BoothId}", booth.BoothOwnerId, booth.Id);
        }
    }

    public async Task<ApiResponse<PaginationResp<ModerationActionHistoryResponse>>> GetBoothHistoryAsync(
        Guid boothId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (await _moderationRepo.GetBoothDetailAsync(boothId, cancellationToken) is null)
            throw AppException.NotFound("Booth was not found.");

        var result = await _moderationRepo.GetHistoryByBoothAsync(boothId, page, pageSize, cancellationToken);
        var items = result.Items.Select(MapHistory).ToList();

        return ApiResponse<PaginationResp<ModerationActionHistoryResponse>>.SuccessResponse(
            PaginationResp<ModerationActionHistoryResponse>.Create(
                items, result.TotalCount, new PaginationReq { Page = page, PageSize = pageSize }));
    }

    // ════════════════════════════════════════════════════════════
    //  Private helpers
    // ════════════════════════════════════════════════════════════

    private static void ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            throw AppException.BadRequest("Reason is required and must be at least 10 characters.");
        if (reason.Length > 1000)
            throw AppException.BadRequest("Reason must not exceed 1000 characters.");
    }

    private static void ValidateModerationStatus(ModerationStatus status)
    {
        if (status != ModerationStatus.Active && status != ModerationStatus.Suspended)
            throw AppException.BadRequest($"Invalid moderation status '{status}'. Only Active or Suspended are allowed.");
    }

    private static MarketModerationOverviewResponse MapMarketOverview(
        NightMarket market, IReadOnlyDictionary<Guid, string> ownerNames,
        IReadOnlyDictionary<Guid, int> boothCounts,
        IReadOnlyDictionary<Guid, int> activeBoothCounts,
        IReadOnlyDictionary<Guid, int> seriousComplaintCounts)
    {
        var ownerName = market.MarketOwnerId.HasValue ? ownerNames.GetValueOrDefault(market.MarketOwnerId.Value) : null;
        return new MarketModerationOverviewResponse
        {
            Id = market.Id,
            MarketOwnerName = ownerName,
            MarketOwnerEmail = market.MarketOwner?.Email,
            MarketName = market.Name,
            Address = market.Address,
            ThumbnailUrl = market.ThumbnailUrl,
            OpeningHours = market.OpeningHours?.ToString(),
            ClosingHours = market.ClosingHours?.ToString(),
            LifecycleStatus = market.Status.ToString(),
            ModerationStatus = market.ModerationStatus.ToString(),
            TotalBooths = boothCounts.GetValueOrDefault(market.Id),
            ActiveBooths = activeBoothCounts.GetValueOrDefault(market.Id),
            AvailableBooths = GetAvailableBoothCount(market, activeBoothCounts.GetValueOrDefault(market.Id)),
            SeriousComplaintCount = seriousComplaintCounts.GetValueOrDefault(market.Id),
            CreatedAt = market.CreatedAt,
            UpdatedAt = market.UpdatedAt
        };
    }

    private static MarketModerationDetailResponse MapMarketDetail(
        NightMarket market,
        int totalBooths,
        int activeBooths,
        int totalComplaints,
        int seriousComplaints,
        List<Complaint> recentComplaints)
    {
        return new MarketModerationDetailResponse
        {
            Id = market.Id,
            MarketOwnerName = market.MarketOwner?.FullName,
            MarketOwnerEmail = market.MarketOwner?.Email,
            MarketName = market.Name,
            Address = market.Address,
            Description = market.Description,
            Latitude = market.Latitude,
            Longitude = market.Longitude,
            ThumbnailUrl = market.ThumbnailUrl,
            OpeningHours = market.OpeningHours?.ToString(),
            ClosingHours = market.ClosingHours?.ToString(),
            OwnerPhone = market.MarketOwner?.Phone,
            LifecycleStatus = market.Status.ToString(),
            ModerationStatus = market.ModerationStatus.ToString(),
            TotalBooths = totalBooths,
            ActiveBooths = activeBooths,
            AvailableBooths = GetAvailableBoothCount(market, activeBooths),
            TotalComplaintCount = totalComplaints,
            SeriousComplaintCount = seriousComplaints,
            CreatedAt = market.CreatedAt,
            UpdatedAt = market.UpdatedAt,
            RecentComplaints = recentComplaints.Select(MapComplaintSummary).ToList()
        };
    }

    private static int GetAvailableBoothCount(NightMarket market, int activeBooths)
    {
        return market.Status == NightMarketStatus.Active
            && market.ModerationStatus == ModerationStatus.Active
            ? activeBooths
            : 0;
    }

    private static BoothModerationOverviewResponse MapBoothOverview(Booth booth, int complaintCount)
    {
        return new BoothModerationOverviewResponse
        {
            Id = booth.Id,
            BoothName = booth.BoothName,
            BoothOwnerName = booth.BoothOwner?.FullName,
            BoothOwnerEmail = booth.BoothOwner?.Email,
            NightMarketName = booth.NightMarket?.Name,
            ZoneName = booth.Zone?.ZoneName,
            SlotNumber = booth.SlotNumber,
            ThumbnailUrl = booth.ThumbnailUrl,
            Status = booth.Status.ToString(),
            AverageRating = booth.AverageRating,
            ComplaintCount = complaintCount,
            CreatedAt = booth.CreatedAt,
            UpdatedAt = booth.UpdatedAt
        };
    }

    private static BoothModerationDetailResponse MapBoothDetail(
        Booth booth, int complaintCount, List<Complaint> recentComplaints)
    {
        return new BoothModerationDetailResponse
        {
            Id = booth.Id,
            BoothName = booth.BoothName,
            BoothOwnerName = booth.BoothOwner?.FullName,
            BoothOwnerEmail = booth.BoothOwner?.Email,
            NightMarketName = booth.NightMarket?.Name,
            ZoneName = booth.Zone?.ZoneName,
            SlotNumber = booth.SlotNumber,
            ThumbnailUrl = booth.ThumbnailUrl,
            Description = booth.Description,
            PhoneNumber = booth.PhoneNumber,
            OpenTime = booth.OpenTime?.ToString(),
            CloseTime = booth.CloseTime?.ToString(),
            Status = booth.Status.ToString(),
            AverageRating = booth.AverageRating,
            ComplaintCount = complaintCount,
            CreatedAt = booth.CreatedAt,
            UpdatedAt = booth.UpdatedAt,
            Documents = (booth.BoothDocuments ?? Enumerable.Empty<BoothDocument>())
                .Concat(booth.Registration?.BoothDocuments ?? Enumerable.Empty<BoothDocument>())
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderBy(d => d.CreatedAt)
                .Select(d => new BoothDocumentResponse
                {
                    Id = d.Id,
                    DocumentType = d.DocumentType.ToString(),
                    FileUrl = d.FileUrl,
                    VerificationStatus = d.VerificationStatus.ToString(),
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                }).ToList(),
            RecentComplaints = recentComplaints.Select(MapComplaintSummary).ToList()
        };
    }

    private static ModerationComplaintSummaryResponse MapComplaintSummary(Complaint complaint)
    {
        var severity = complaint.Status == ComplaintStatus.Pending
            ? "Pending"
            : complaint.ResolutionAction == ComplaintResolutionAction.SuspendBooth
                ? "High"
            : complaint.ResolutionAction == ComplaintResolutionAction.Warning
                ? "Medium"
                : "Low";

        return new ModerationComplaintSummaryResponse
        {
            Id = complaint.Id,
            Title = complaint.Title,
            Description = complaint.Description,
            Severity = severity,
            Status = complaint.Status.ToString(),
            CreatedAt = complaint.CreatedAt
        };
    }

    private static ModerationActionHistoryResponse MapHistory(ModerationActionHistory history)
    {
        return new ModerationActionHistoryResponse
        {
            Id = history.Id,
            BoothId = history.BoothId,
            NightMarketId = history.NightMarketId,
            AdminId = history.AdminId,
            AdminName = history.AdminName,
            PreviousStatus = history.PreviousStatus,
            NewStatus = history.NewStatus,
            Reason = history.Reason,
            Source = history.Source.ToString(),
            ComplaintId = history.ComplaintId,
            CreatedAt = history.CreatedAt
        };
    }
}
