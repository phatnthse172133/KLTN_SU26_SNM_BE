using ApplicationLayer.DTOs;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Support;

public interface ISupportTicketService
{
    Task<ApiResponse<SupportTicketDetail>> CreateAsync(Guid userId, string role, CreateSupportTicketRequest request, CancellationToken ct = default);
    Task<ApiResponse<PaginationResp<SupportTicketListItem>>> GetMineAsync(Guid userId, PaginationReq query, CancellationToken ct = default);
    Task<ApiResponse<SupportTicketDetail>> GetMineDetailAsync(Guid userId, Guid ticketId, CancellationToken ct = default);
    Task<ApiResponse<SupportTicketDetail>> ReplyAsync(Guid userId, string role, Guid ticketId, SendSupportMessageRequest request, bool isAdmin, CancellationToken ct = default);
    Task<ApiResponse<SupportAttachmentResponse>> AddAttachmentAsync(Guid userId, Guid ticketId, Stream stream, string fileName, string contentType, long length, bool isAdmin, CancellationToken ct = default);
    Task<ApiResponse<PaginationResp<SupportTicketListItem>>> GetAdminAsync(SupportTicketQuery query, CancellationToken ct = default);
    Task<ApiResponse<SupportTicketDetail>> GetAdminDetailAsync(Guid ticketId, CancellationToken ct = default);
    Task<ApiResponse<SupportTicketDetail>> UpdateAsync(Guid adminId, Guid ticketId, UpdateSupportTicketRequest request, CancellationToken ct = default);
    Task<ApiResponse<SupportMetricsResponse>> GetMetricsAsync(CancellationToken ct = default);
}
