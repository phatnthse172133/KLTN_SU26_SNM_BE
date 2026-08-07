using DomainLayer.Common;
using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepositories;

public interface ISupportTicketRepository
{
    Task AddAsync(SupportTicket ticket, CancellationToken ct = default);
    Task AddAttachmentAsync(SupportAttachment attachment, CancellationToken ct = default);
    Task<SupportTicket?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<SupportTicket>> GetMineAsync(Guid requesterId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<SupportTicket>> GetAdminAsync(string? keyword, string? status, string? category, string? requesterRole, bool? overdue, int page, int pageSize, CancellationToken ct = default);
    Task<SupportMetricsSnapshot> GetMetricsAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed record SupportMetricsSnapshot(int Open, int InProgress, int WaitingForRequester, int Overdue, int ResolvedToday);
