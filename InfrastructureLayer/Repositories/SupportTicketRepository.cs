using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class SupportTicketRepository : ISupportTicketRepository
{
    private readonly SNMDbContext _context;
    public SupportTicketRepository(SNMDbContext context) => _context = context;

    public Task AddAsync(SupportTicket ticket, CancellationToken ct = default)
        => _context.SupportTickets.AddAsync(ticket, ct).AsTask();

    public Task AddAttachmentAsync(SupportAttachment attachment, CancellationToken ct = default)
        => _context.SupportAttachments.AddAsync(attachment, ct).AsTask();

    public Task<SupportTicket?> GetDetailAsync(Guid id, CancellationToken ct = default)
        => _context.SupportTickets
            .Include(t => t.Requester)
            .Include(t => t.AssignedAdmin)
            .Include(t => t.Messages).ThenInclude(m => m.Sender)
            .Include(t => t.Attachments)
            .Include(t => t.StatusHistory).ThenInclude(h => h.Actor)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<PagedResult<SupportTicket>> GetMineAsync(Guid requesterId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.SupportTickets.Include(t => t.Requester).Where(t => t.RequesterId == requesterId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<SupportTicket>(items, total);
    }

    public async Task<PagedResult<SupportTicket>> GetAdminAsync(string? keyword, string? status, string? category, string? requesterRole, bool? overdue, int page, int pageSize, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = _context.SupportTickets.Include(t => t.Requester).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLower();
            query = query.Where(t => t.TicketCode.ToLower().Contains(term) || t.Title.ToLower().Contains(term) || t.Requester.Email.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var requested = status.Trim();
            query = requested switch
            {
                "Pending" => query.Where(t => t.Status == "Pending" || t.Status == "Open"),
                "InProgress" => query.Where(t => t.Status == "InProgress" || t.Status == "WaitingForRequester"),
                "Resolved" => query.Where(t => t.Status == "Resolved" || t.Status == "Closed"),
                _ => query.Where(t => t.Status == requested)
            };
        }
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(t => t.Category == category);
        if (!string.IsNullOrWhiteSpace(requesterRole)) query = query.Where(t => t.RequesterRole == requesterRole);
        if (overdue == true) query = query.Where(t => t.FirstRespondedAt == null && t.DueAt < now && t.Status != "Resolved" && t.Status != "Rejected" && t.Status != "Closed");
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(t => t.FirstRespondedAt == null && t.DueAt < now ? 0 : 1).ThenBy(t => t.DueAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<SupportTicket>(items, total);
    }

    public async Task<SupportMetricsSnapshot> GetMetricsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        return new SupportMetricsSnapshot(
            await _context.SupportTickets.CountAsync(t => t.Status == "Pending" || t.Status == "Open", ct),
            await _context.SupportTickets.CountAsync(t => t.Status == "InProgress", ct),
            await _context.SupportTickets.CountAsync(t => t.Status == "WaitingForRequester", ct),
            await _context.SupportTickets.CountAsync(t => t.FirstRespondedAt == null && t.DueAt < now && t.Status != "Resolved" && t.Status != "Rejected" && t.Status != "Closed", ct),
            await _context.SupportTickets.CountAsync(t => t.ResolvedAt >= today && t.ResolvedAt < today.AddDays(1), ct));
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
