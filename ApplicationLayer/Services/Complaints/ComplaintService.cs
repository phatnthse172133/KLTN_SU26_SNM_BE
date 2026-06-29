using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Complaints;

public class ComplaintService : IComplaintService
{
    private readonly IComplaintRepository _complaints;
    private readonly IBoothRepository _booths;
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;

    public ComplaintService(
        IComplaintRepository complaints,
        IBoothRepository booths,
        IOrderRepository orders,
        IMapper mapper)
    {
        _complaints = complaints;
        _booths = booths;
        _orders = orders;
        _mapper = mapper;
    }

    public async Task<ApiResponse<ComplaintResponse>> CreateAsync(Guid customerId, CreateComplaintRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOrderForBoothAsync(customerId, request.OrderId, request.BoothId);

        if (await _complaints.HasActiveComplaintAsync(customerId, request.BoothId, request.OrderId))
            throw AppException.Conflict("There is already an active complaint for this order and booth.");

        var now = DateTime.UtcNow;
        var complaint = _mapper.Map<Complaint>(request);
        complaint.Id = Guid.NewGuid();
        complaint.CustomerId = customerId;
        complaint.Status = ComplaintStatus.Submitted;
        complaint.CreatedAt = now;
        complaint.UpdatedAt = now;

        await _complaints.AddAsync(complaint);

        var images = request.Images
            .Where(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
            .Select(i => new ComplaintImage
            {
                Id = Guid.NewGuid(),
                ComplaintId = complaint.Id,
                ImageUrl = i.ImageUrl.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();

        if (images.Count > 0)
        {
            await _complaints.AddImagesAsync(images);
        }

        await _complaints.SaveChangesAsync();

        var response = _mapper.Map<ComplaintResponse>(complaint);
        response.ImageUrls = images.Select(i => i.ImageUrl).ToList();
        return ApiResponse<ComplaintResponse>.SuccessResponse(response, "Complaint submitted successfully.");
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetMineAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _complaints.GetPagedByCustomerWithImagesAsync(
            customerId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            _mapper.MapPage<Complaint, ComplaintResponse>(page, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _complaints.GetPagedWithImagesAsync(
            pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            _mapper.MapPage<Complaint, ComplaintResponse>(page, pagination));
    }

    public async Task<ApiResponse<PaginationResp<ComplaintResponse>>> GetByBoothAsync(Guid ownerId, Guid boothId, PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null) 
            throw AppException.NotFound("Booth was not found.");

        if (booth.BoothOwnerId != ownerId)
            throw AppException.Forbidden("You do not have permission to view this booth's complaints.");

        var page = await _complaints.GetPagedByBoothWithImagesAsync(
            boothId, pagination.Page, pagination.PageSize, cancellationToken);
        return ApiResponse<PaginationResp<ComplaintResponse>>.SuccessResponse(
            _mapper.MapPage<Complaint, ComplaintResponse>(page, pagination));
    }

    public async Task<ApiResponse<ComplaintResponse>> UpdateStatusAsync(Guid complaintId, UpdateComplaintStatusRequest request, CancellationToken cancellationToken = default)
    {
        var complaint = await _complaints.GetByIdAsync(complaintId);
        if (complaint is null) 
            throw AppException.NotFound("Complaint was not found.");

        ValidateStatusTransition(complaint.Status, request.Status);
        ValidateResolutionRequest(request);

        complaint.Status = request.Status;
        ApplyResolution(complaint, request);
        complaint.UpdatedAt = DateTime.UtcNow;

        if (request.Status == ComplaintStatus.Resolved)
        {
            await ApplyBoothPenaltyAsync(complaint.BoothId, request.ResolutionAction!.Value);
        }

        _complaints.Update(complaint);
        await _complaints.SaveChangesAsync();

        var response = await _complaints.GetWithImagesByIdAsync(complaint.Id);
        return ApiResponse<ComplaintResponse>.SuccessResponse(ToResponse(response!), "Complaint status updated successfully.");
    }

    private async Task ValidateOrderForBoothAsync(Guid customerId, Guid orderId, Guid boothId)
    {
        if (await _booths.GetByIdAsync(boothId) is null)
            throw AppException.NotFound("Booth was not found.");

        var order = await _orders.GetByCustomerAsync(customerId, orderId);
        if (order is null)
            throw AppException.NotFound("Order was not found.");

        if (!await _orders.ContainsBoothItemsAsync(orderId, boothId))
            throw AppException.BadRequest("Order does not contain items from this booth.");
    }

    private static void ValidateStatusTransition(ComplaintStatus currentStatus, ComplaintStatus nextStatus)
    {
        if (currentStatus == nextStatus)
            return;

        var allowed = currentStatus switch
        {
            ComplaintStatus.Submitted => nextStatus is ComplaintStatus.UnderInvestigation or ComplaintStatus.Resolved or ComplaintStatus.Rejected,
            ComplaintStatus.UnderInvestigation => nextStatus is ComplaintStatus.Resolved or ComplaintStatus.Rejected or ComplaintStatus.Closed,
            ComplaintStatus.Resolved => nextStatus is ComplaintStatus.Closed,
            ComplaintStatus.Rejected => nextStatus is ComplaintStatus.Closed,
            ComplaintStatus.Closed => false,
            _ => false
        };

        if (!allowed)
            throw AppException.BadRequest($"Cannot change complaint status from {currentStatus} to {nextStatus}.");
    }

    private static void ValidateResolutionRequest(UpdateComplaintStatusRequest request)
    {
        var adminResponse = TextHelper.NormalizeOptionalText(request.AdminResponse);
        var policyViolation = TextHelper.NormalizeOptionalText(request.PolicyViolation);

        if (request.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected && adminResponse is null)
            throw AppException.BadRequest("Admin response is required when resolving or rejecting a complaint.");

        if (request.Status == ComplaintStatus.Resolved)
        {
            if (request.ResolutionAction is null)
                throw AppException.BadRequest("Resolution action is required when resolving a complaint.");

            if (request.ResolutionAction != ComplaintResolutionAction.NoViolation && policyViolation is null)
                throw AppException.BadRequest("Policy violation is required when applying a penalty.");
        }

        if (request.Status == ComplaintStatus.Rejected && request.ResolutionAction is not null and not ComplaintResolutionAction.NoViolation)
            throw AppException.BadRequest("Rejected complaints cannot apply a booth penalty.");

        if (request.Status == ComplaintStatus.UnderInvestigation && (request.ResolutionAction is not null || policyViolation is not null))
            throw AppException.BadRequest("Resolution action and policy violation can only be set when a complaint is resolved.");
    }

    private static void ApplyResolution(Complaint complaint, UpdateComplaintStatusRequest request)
    {
        complaint.AdminResponse = TextHelper.NormalizeOptionalText(request.AdminResponse);

        if (request.Status == ComplaintStatus.Resolved)
        {
            complaint.ResolutionAction = request.ResolutionAction;
            complaint.PolicyViolation = TextHelper.NormalizeOptionalText(request.PolicyViolation);
            return;
        }

        if (request.Status == ComplaintStatus.Rejected)
        {
            complaint.ResolutionAction = ComplaintResolutionAction.NoViolation;
            complaint.PolicyViolation = null;
        }
    }

    private async Task ApplyBoothPenaltyAsync(Guid boothId, ComplaintResolutionAction action)
    {
        var booth = await _booths.GetByIdAsync(boothId);
        if (booth is null)
            throw AppException.NotFound("Booth was not found.");

        if (action == ComplaintResolutionAction.SuspendBooth)
        {
            booth.Status = BoothStatus.Suspended;
            booth.UpdatedAt = DateTime.UtcNow;
            _booths.Update(booth);
        }

        if (action == ComplaintResolutionAction.CloseBooth)
        {
            booth.Status = BoothStatus.Closed;
            booth.UpdatedAt = DateTime.UtcNow;
            _booths.Update(booth);
        }
    }

    private ComplaintResponse ToResponse(Complaint complaint)
        => _mapper.Map<ComplaintResponse>(complaint);
}
