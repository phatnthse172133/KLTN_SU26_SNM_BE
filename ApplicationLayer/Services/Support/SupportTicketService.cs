using ApplicationLayer.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Realtime;
using ApplicationLayer.Services.Storage;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Support;

public class SupportTicketService : ISupportTicketService
{
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
    { "Account", "Subscription", "Payment", "Booth", "NightMarket", "LayoutAssignment", "Menu", "Order", "Promotion", "TechnicalIssue", "Other" };
    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase)
    { "Open", "InProgress", "WaitingForRequester", "Resolved", "Closed" };
    private readonly ISupportTicketRepository _repository;
    private readonly IFileStorageService _storage;
    private readonly INotificationService _notifications;
    private readonly IRealtimeEventPublisher _eventPublisher;

    public SupportTicketService(ISupportTicketRepository repository, IFileStorageService storage, INotificationService notifications, IRealtimeEventPublisher eventPublisher)
    {
        _repository = repository;
        _storage = storage;
        _notifications = notifications;
        _eventPublisher = eventPublisher;
    }

    public async Task<ApiResponse<SupportTicketDetail>> CreateAsync(Guid userId, string role, CreateSupportTicketRequest request, CancellationToken ct = default)
    {
        if (!Categories.Contains(request.Category))
            throw AppException.BadRequest("Select a valid support category.", "SUPPORT_CATEGORY_INVALID");
        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            TicketCode = $"SUP-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..19].ToUpperInvariant(),
            RequesterId = userId,
            RequesterRole = role,
            BoothId = request.BoothId,
            NightMarketId = request.NightMarketId,
            Category = NormalizeCategory(request.Category),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            PageUrl = string.IsNullOrWhiteSpace(request.PageUrl) ? null : request.PageUrl.Trim(),
            Status = "Open",
            Priority = "Normal",
            DueAt = now.AddHours(24),
            CreatedAt = now,
            UpdatedAt = now,
        };
        ticket.StatusHistory.Add(new SupportStatusHistory { Id = Guid.NewGuid(), ActorId = userId, FromStatus = null, ToStatus = "Open", Note = "Support request created.", CreatedAt = now });
        await _repository.AddAsync(ticket, ct);
        await _repository.SaveChangesAsync(ct);
        await NotifySafelyAsync(new RoleNotificationMessage("Admin", NotificationType.System, "New support request", $"{ticket.TicketCode}: {ticket.Title}", ReferenceType: "SupportTicket", ReferenceId: ticket.Id), ct);
        try
        {
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "SupportTicketCreated",
                Role = "Admin",
                Payload = new { ticketId = ticket.Id, ticketCode = ticket.TicketCode, title = ticket.Title, category = ticket.Category, requesterId = userId }
            });
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "SupportTicketCreated",
                RecipientId = userId,
                GroupName = RealtimeGroups.SupportTicket(ticket.Id),
                Payload = new { ticketId = ticket.Id, ticketCode = ticket.TicketCode }
            });
        }
        catch { }
        var detail = await _repository.GetDetailAsync(ticket.Id, ct) ?? ticket;
        return ApiResponse<SupportTicketDetail>.SuccessResponse(MapDetail(detail, false), "Your support request has been sent. We will respond within 24 hours.");
    }

    public async Task<ApiResponse<PaginationResp<SupportTicketListItem>>> GetMineAsync(Guid userId, PaginationReq query, CancellationToken ct = default)
    {
        var result = await _repository.GetMineAsync(userId, query.Page, query.PageSize, ct);
        return ApiResponse<PaginationResp<SupportTicketListItem>>.SuccessResponse(PaginationResp<SupportTicketListItem>.Create(result.Items.Select(MapList).ToList(), result.TotalCount, query));
    }

    public async Task<ApiResponse<SupportTicketDetail>> GetMineDetailAsync(Guid userId, Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await RequireTicketAsync(ticketId, ct);
        if (ticket.RequesterId != userId) throw AppException.Forbidden("You can only view your own support requests.", "SUPPORT_TICKET_ACCESS_DENIED");
        return ApiResponse<SupportTicketDetail>.SuccessResponse(MapDetail(ticket, false));
    }

    public async Task<ApiResponse<SupportTicketDetail>> ReplyAsync(Guid userId, string role, Guid ticketId, SendSupportMessageRequest request, bool isAdmin, CancellationToken ct = default)
    {
        var ticket = await RequireTicketAsync(ticketId, ct);
        if (!isAdmin && ticket.RequesterId != userId) throw AppException.Forbidden("You can only reply to your own support requests.", "SUPPORT_TICKET_ACCESS_DENIED");
        if (!isAdmin && request.IsInternalNote) throw AppException.Forbidden("Internal notes are available to administrators only.", "SUPPORT_INTERNAL_NOTE_FORBIDDEN");
        if (ticket.Status == "Closed") throw AppException.Conflict("This support request is closed. Reopen it before sending a reply.", "SUPPORT_TICKET_CLOSED");
        var now = DateTime.UtcNow;
        ticket.Messages.Add(new SupportMessage { Id = Guid.NewGuid(), TicketId = ticket.Id, SenderId = userId, SenderRole = role, Body = request.Body.Trim(), IsInternalNote = isAdmin && request.IsInternalNote, CreatedAt = now });
        if (isAdmin && !request.IsInternalNote && ticket.FirstRespondedAt is null) ticket.FirstRespondedAt = now;
        var previousStatus = ticket.Status;
        if (isAdmin && !request.IsInternalNote && ticket.Status == "Open") ticket.Status = "InProgress";
        if (!isAdmin && ticket.Status == "WaitingForRequester") ticket.Status = "InProgress";
        if (!string.Equals(previousStatus, ticket.Status, StringComparison.Ordinal))
        {
            ticket.StatusHistory.Add(new SupportStatusHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                ActorId = userId,
                FromStatus = previousStatus,
                ToStatus = ticket.Status,
                Note = isAdmin ? "Administrator replied." : "Requester replied.",
                CreatedAt = now
            });
        }
        ticket.UpdatedAt = now;
        await _repository.SaveChangesAsync(ct);
        if (!request.IsInternalNote)
        {
            if (isAdmin) await NotifySafelyAsync(new NotificationMessage(ticket.RequesterId, NotificationType.System, "Support replied", $"An administrator replied to {ticket.TicketCode}.", ReferenceType: "SupportTicket", ReferenceId: ticket.Id), ct);
            else await NotifySafelyAsync(new RoleNotificationMessage("Admin", NotificationType.System, "Support request updated", $"The requester replied to {ticket.TicketCode}.", ReferenceType: "SupportTicket", ReferenceId: ticket.Id), ct);
            try
            {
                await _eventPublisher.PublishAsync(new RealtimeEvent
                {
                    EventType = "SupportTicketReplied",
                    GroupName = RealtimeGroups.SupportTicket(ticket.Id),
                    Payload = new { ticketId = ticket.Id, ticketCode = ticket.TicketCode, senderId = userId, senderRole = role, isAdmin }
                });
                if (isAdmin)
                    await _eventPublisher.PublishAsync(new RealtimeEvent { EventType = "SupportTicketReplied", RecipientId = ticket.RequesterId, Payload = new { ticketId = ticket.Id, ticketCode = ticket.TicketCode } });
                else
                    await _eventPublisher.PublishAsync(new RealtimeEvent { EventType = "SupportTicketReplied", Role = "Admin", Payload = new { ticketId = ticket.Id, ticketCode = ticket.TicketCode } });
            }
            catch { }
        }
        return ApiResponse<SupportTicketDetail>.SuccessResponse(MapDetail(await RequireTicketAsync(ticketId, ct), isAdmin));
    }

    public async Task<ApiResponse<SupportAttachmentResponse>> AddAttachmentAsync(Guid userId, Guid ticketId, Stream stream, string fileName, string contentType, long length, bool isAdmin, CancellationToken ct = default)
    {
        var ticket = await RequireTicketAsync(ticketId, ct);
        if (!isAdmin && ticket.RequesterId != userId) throw AppException.Forbidden("You can only add evidence to your own support requests.", "SUPPORT_TICKET_ACCESS_DENIED");
        if (ticket.Attachments.Count >= 5) throw AppException.BadRequest("A support request can contain up to 5 evidence files.", "SUPPORT_ATTACHMENT_LIMIT");
        var url = await _storage.SaveDocumentAsync("support-evidence", stream, fileName, contentType, length, ct);
        var attachment = new SupportAttachment { Id = Guid.NewGuid(), TicketId = ticket.Id, FileUrl = url, OriginalFileName = Path.GetFileName(fileName), ContentType = contentType, FileSize = length, CreatedAt = DateTime.UtcNow };
        await _repository.AddAttachmentAsync(attachment, ct);
        ticket.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync(ct);
        try
        {
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "SupportAttachmentAdded",
                GroupName = RealtimeGroups.SupportTicket(ticket.Id),
                Payload = new { ticketId = ticket.Id, attachmentId = attachment.Id, fileName = attachment.OriginalFileName }
            });
        }
        catch { }
        return ApiResponse<SupportAttachmentResponse>.SuccessResponse(MapAttachment(attachment), "Evidence uploaded successfully.");
    }

    public async Task<ApiResponse<PaginationResp<SupportTicketListItem>>> GetAdminAsync(SupportTicketQuery query, CancellationToken ct = default)
    {
        var result = await _repository.GetAdminAsync(query.Keyword, query.Status, query.Category, query.RequesterRole, query.Overdue, query.Page, query.PageSize, ct);
        return ApiResponse<PaginationResp<SupportTicketListItem>>.SuccessResponse(PaginationResp<SupportTicketListItem>.Create(result.Items.Select(MapList).ToList(), result.TotalCount, query));
    }

    public async Task<ApiResponse<SupportTicketDetail>> GetAdminDetailAsync(Guid ticketId, CancellationToken ct = default)
        => ApiResponse<SupportTicketDetail>.SuccessResponse(MapDetail(await RequireTicketAsync(ticketId, ct), true));

    public async Task<ApiResponse<SupportTicketDetail>> UpdateAsync(Guid adminId, Guid ticketId, UpdateSupportTicketRequest request, CancellationToken ct = default)
    {
        if (!Statuses.Contains(request.Status)) throw AppException.BadRequest("Select a valid support status.", "SUPPORT_STATUS_INVALID");
        var ticket = await RequireTicketAsync(ticketId, ct);
        var normalized = NormalizeStatus(request.Status);
        var previous = ticket.Status;
        var now = DateTime.UtcNow;
        ticket.Status = normalized;
        ticket.AssignedAdminId = request.AssignedAdminId ?? ticket.AssignedAdminId ?? adminId;
        if (normalized == "Resolved") ticket.ResolvedAt = now;
        if (normalized == "Closed") ticket.ClosedAt = now;
        if (normalized != "Resolved") ticket.ResolvedAt = null;
        if (normalized != "Closed") ticket.ClosedAt = null;
        ticket.UpdatedAt = now;
        ticket.StatusHistory.Add(new SupportStatusHistory { Id = Guid.NewGuid(), TicketId = ticket.Id, ActorId = adminId, FromStatus = previous, ToStatus = normalized, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), CreatedAt = now });
        await _repository.SaveChangesAsync(ct);
        await NotifySafelyAsync(new NotificationMessage(ticket.RequesterId, NotificationType.System, "Support status updated", $"{ticket.TicketCode} is now {ToDisplayStatus(normalized)}.", ReferenceType: "SupportTicket", ReferenceId: ticket.Id), ct);
        try
        {
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "SupportTicketStatusChanged",
                RecipientId = ticket.RequesterId,
                GroupName = RealtimeGroups.SupportTicket(ticket.Id),
                Payload = new { ticketId = ticket.Id, ticketCode = ticket.TicketCode, oldStatus = previous, newStatus = normalized }
            });
            await _eventPublisher.PublishAsync(new RealtimeEvent
            {
                EventType = "SupportTicketStatusChanged",
                Role = "Admin",
                Payload = new { ticketId = ticket.Id, ticketCode = ticket.TicketCode, newStatus = normalized }
            });
        }
        catch { }
        return ApiResponse<SupportTicketDetail>.SuccessResponse(MapDetail(await RequireTicketAsync(ticketId, ct), true));
    }

    public async Task<ApiResponse<SupportMetricsResponse>> GetMetricsAsync(CancellationToken ct = default)
    {
        var value = await _repository.GetMetricsAsync(ct);
        return ApiResponse<SupportMetricsResponse>.SuccessResponse(new SupportMetricsResponse { Open = value.Open, InProgress = value.InProgress, WaitingForRequester = value.WaitingForRequester, Overdue = value.Overdue, ResolvedToday = value.ResolvedToday });
    }

    private async Task<SupportTicket> RequireTicketAsync(Guid id, CancellationToken ct)
        => await _repository.GetDetailAsync(id, ct) ?? throw AppException.NotFound("This support request could not be found.", "SUPPORT_TICKET_NOT_FOUND");

    private async Task NotifySafelyAsync(NotificationMessage message, CancellationToken ct) { try { await _notifications.NotifyAsync(message, ct); } catch { } }
    private async Task NotifySafelyAsync(RoleNotificationMessage message, CancellationToken ct) { try { await _notifications.NotifyRoleAsync(message, ct); } catch { } }
    private static string NormalizeCategory(string value) => Categories.First(c => c.Equals(value, StringComparison.OrdinalIgnoreCase));
    private static string NormalizeStatus(string value) => Statuses.First(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
    private static string ToDisplayStatus(string value) => value switch { "InProgress" => "In Progress", "WaitingForRequester" => "Waiting for Requester", _ => value };
    private static SupportTicketListItem MapList(SupportTicket t) => new() { Id = t.Id, TicketCode = t.TicketCode, Title = t.Title, Category = t.Category, Status = t.Status, Priority = t.Priority, RequesterName = t.Requester?.FullName ?? "User", RequesterEmail = t.Requester?.Email ?? "", RequesterRole = t.RequesterRole, DueAt = t.DueAt, IsOverdue = t.FirstRespondedAt is null && t.DueAt < DateTime.UtcNow && t.Status != "Closed", CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt };
    private static SupportAttachmentResponse MapAttachment(SupportAttachment a) => new() { Id = a.Id, FileUrl = a.FileUrl, OriginalFileName = a.OriginalFileName, ContentType = a.ContentType, FileSize = a.FileSize };
    private static SupportTicketDetail MapDetail(SupportTicket t, bool includeInternal) => new()
    {
        Id = t.Id, TicketCode = t.TicketCode, Title = t.Title, Category = t.Category, Status = t.Status, Priority = t.Priority,
        RequesterName = t.Requester?.FullName ?? "User", RequesterEmail = t.Requester?.Email ?? "", RequesterRole = t.RequesterRole,
        DueAt = t.DueAt, IsOverdue = t.FirstRespondedAt is null && t.DueAt < DateTime.UtcNow && t.Status != "Closed", CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt,
        Description = t.Description, PageUrl = t.PageUrl, BoothId = t.BoothId, NightMarketId = t.NightMarketId, AssignedAdminId = t.AssignedAdminId,
        AssignedAdminName = t.AssignedAdmin?.FullName, FirstRespondedAt = t.FirstRespondedAt, ResolvedAt = t.ResolvedAt,
        Messages = t.Messages.Where(m => includeInternal || !m.IsInternalNote).OrderBy(m => m.CreatedAt).Select(m => new SupportMessageResponse { Id = m.Id, SenderName = m.Sender?.FullName ?? m.SenderRole, SenderRole = m.SenderRole, Body = m.Body, IsInternalNote = m.IsInternalNote, CreatedAt = m.CreatedAt }).ToList(),
        Attachments = t.Attachments.OrderBy(a => a.CreatedAt).Select(MapAttachment).ToList(),
        StatusHistory = t.StatusHistory.OrderBy(h => h.CreatedAt).Select(h => new SupportStatusHistoryResponse { FromStatus = h.FromStatus, ToStatus = h.ToStatus, Note = h.Note, ActorName = h.Actor?.FullName ?? "User", CreatedAt = h.CreatedAt }).ToList()
    };
}
