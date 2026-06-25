using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Complaints;

public interface IComplaintService
{
    Task<ApiResponse<ComplaintResponse>> CreateAsync(Guid customerId, CreateComplaintRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetByBoothAsync(Guid ownerId, Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default);
    Task<ApiResponse<ComplaintResponse>> UpdateStatusAsync(Guid complaintId, UpdateComplaintStatusRequest request, CancellationToken cancellationToken = default);
}
