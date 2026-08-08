using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Complaints;

public interface IComplaintService
{
    Task<ApiResponse<ComplaintResponse>> CreateAsync(Guid customerId, CreateComplaintRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintResponse>> GetMineDetailAsync(Guid customerId, Guid complaintId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintResponse>> WithdrawAsync(Guid customerId, Guid complaintId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintResponse>> AddEvidenceAsync(Guid customerId, Guid complaintId, AddComplaintEvidenceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintImageUploadResponse>> UploadImageAsync(Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllAsync(PaginationReq pagination, DomainLayer.Enums.GeneralEnum.ComplaintStatus? status = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllFilteredAsync(AdminComplaintQueryRequest query, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetByBoothAsync(Guid ownerId, Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetByMarketOwnerAsync(Guid marketOwnerId, MarketOwnerComplaintQueryRequest query, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintResponse>> GetDetailForMarketOwnerAsync(Guid marketOwnerId, Guid complaintId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintCountsResponse>> GetCountsByMarketOwnerAsync(Guid marketOwnerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintResponse>> UpdateStatusAsync(Guid complaintId, UpdateComplaintStatusRequest request, CancellationToken cancellationToken = default, Guid? actorId = null, string? actorRole = null);
    Task<ApiResponse<ComplaintCountsResponse>> GetCountsAsync(CancellationToken cancellationToken = default);
}
