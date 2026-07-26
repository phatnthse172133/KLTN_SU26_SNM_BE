using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Complaints;

public interface IComplaintService
{
    Task<ApiResponse<ComplaintResponse>> CreateAsync(Guid customerId, CreateComplaintRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllAsync(PaginationReq pagination, ComplaintStatus? status = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllFilteredAsync(AdminComplaintQueryRequest query, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetByBoothAsync(Guid ownerId, Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetByMarketOwnerAsync(Guid marketOwnerId, MarketOwnerComplaintQueryRequest query, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintResponse>> GetDetailForMarketOwnerAsync(Guid marketOwnerId, Guid complaintId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintCountsResponse>> GetCountsByMarketOwnerAsync(Guid marketOwnerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintResponse>> UpdateStatusAsync(Guid complaintId, UpdateComplaintStatusRequest request, CancellationToken cancellationToken = default, Guid? actorId = null);
    Task<ApiResponse<ComplaintCountsResponse>> GetCountsAsync(CancellationToken cancellationToken = default);
}
