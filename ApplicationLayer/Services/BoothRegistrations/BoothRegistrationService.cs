using AutoMapper;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.BoothRegistrations;

public class BoothRegistrationService : IBoothRegistrationService
{
    private readonly IGenericRepository<BoothRegistration> _registrations;
    private readonly IGenericRepository<BoothDocument> _documents;
    private readonly IGenericRepository<Booth> _booths;
    private readonly IGenericRepository<NightMarket> _markets;
    private readonly IGenericRepository<Zone> _zones;
    private readonly IGenericRepository<LayoutNode> _nodes;
    private readonly IGenericRepository<User> _users;
    private readonly IGenericRepository<Role> _roles;
    private readonly IMapper _mapper;

    public BoothRegistrationService(IGenericRepository<BoothRegistration> registrations, IGenericRepository<BoothDocument> documents, IGenericRepository<Booth> booths, IGenericRepository<NightMarket> markets, IGenericRepository<Zone> zones, IGenericRepository<LayoutNode> nodes, IGenericRepository<User> users, IGenericRepository<Role> roles, IMapper mapper)
    {
        _registrations = registrations;
        _documents = documents;
        _booths = booths;
        _markets = markets;
        _zones = zones;
        _nodes = nodes;
        _users = users;
        _roles = roles;
        _mapper = mapper;
    }

    public async Task<ApiResponse<BoothRegistrationResponse>> CreateAsync(Guid ownerId, CreateBoothRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        if (!await IsBoothOwnerAsync(ownerId))
            throw AppException.Forbidden("Only accounts registered as BoothOwner can submit a booth registration.");

        var validationError = await ValidateRequestAsync(request);
        if (validationError is not null) 
            throw AppException.BadRequest(validationError);

        if (await _registrations.AnyAsync(r => r.OwnerId == ownerId && r.Status == BoothRegistrationStatus.PendingReview))
            throw AppException.Conflict("You already have a booth registration pending review.");

        var now = DateTime.UtcNow;
        var registration = _mapper.Map<BoothRegistration>(request);
        registration.Id = Guid.NewGuid(); registration.OwnerId = ownerId; registration.Status = BoothRegistrationStatus.PendingReview;
        registration.CreatedAt = now; registration.UpdatedAt = now;
        await _registrations.AddAsync(registration);

        var documents = CreateDocuments(registration.Id, request.Documents, now);
        await _documents.AddRangeAsync(documents);

        await _registrations.SaveChangesAsync();
        return ApiResponse<BoothRegistrationResponse>.SuccessResponse(ToResponse(registration, documents), "Booth registration submitted successfully.");
    }

    public async Task<ApiResponse<object>> GetMineAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => ApiResponse<object>.SuccessResponse(await ToResponsesAsync(await _registrations.FindAsync(r => r.OwnerId == ownerId)));

    public async Task<ApiResponse<PaginationResp<BoothRegistrationResponse>>> GetPendingAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _registrations.GetPagedAsync(r => r.Status == BoothRegistrationStatus.PendingReview, pagination.Page, pagination.PageSize, r => r.CreatedAt);

        return ApiResponse<PaginationResp<BoothRegistrationResponse>>.SuccessResponse(new PaginationResp<BoothRegistrationResponse>
        {
            Items = await ToResponsesAsync(items), Page = pagination.Page, PageSize = pagination.PageSize, Total = total
        });
    }

    public async Task<ApiResponse<BoothRegistrationResponse>> ReviewAsync(Guid registrationId, ReviewBoothRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var registration = await _registrations.GetByIdAsync(registrationId);
        if (registration is null) 
            throw AppException.NotFound("Booth registration was not found.");

        if (registration.Status != BoothRegistrationStatus.PendingReview) 
            throw AppException.Conflict("This registration has already been processed.");

        if (!request.Approved && string.IsNullOrWhiteSpace(request.RejectReason)) 
            throw AppException.BadRequest("A rejection reason is required.");

        if (request.Approved && request.ZoneId.HasValue && !(await IsZoneInMarketAsync(request.ZoneId.Value, registration.RequestedNightMarketId)))
            throw AppException.BadRequest("The assigned zone does not belong to the night market.");

        if (request.Approved && !await IsBoothOwnerAsync(registration.OwnerId))
            throw AppException.Conflict("The registration owner is not a BoothOwner account and cannot be approved.");

        var now = DateTime.UtcNow;
        registration.Status = request.Approved ? BoothRegistrationStatus.Approved : BoothRegistrationStatus.Rejected;
        registration.RejectReason = request.Approved ? null : request.RejectReason!.Trim(); registration.UpdatedAt = now;

        var docs = (await _documents.FindAsync(d => d.RegistrationId == registration.Id)).ToList();
        foreach (var doc in docs) { doc.VerificationStatus = request.Approved ? BoothDocumentStatus.Verified : BoothDocumentStatus.Rejected; doc.UpdatedAt = now; }
        _documents.UpdateRange(docs);
        if (request.Approved)
        {
            var booth = new Booth { Id = Guid.NewGuid(), RegistrationId = registration.Id, NightMarketId = registration.RequestedNightMarketId,
                BoothOwnerId = registration.OwnerId, ZoneId = request.ZoneId ?? registration.PreferredZoneId, BoothName = registration.BoothName,
                Description = registration.Description, PhoneNumber = registration.Phone, SlotNumber = request.SlotNumber?.Trim(),
                MapPositionX = request.MapPositionX, MapPositionY = request.MapPositionY, Status = BoothStatus.Active, CreatedAt = now, UpdatedAt = now };

            await _booths.AddAsync(booth);
            registration.Booth = booth;
        }
        _registrations.Update(registration);

        await _registrations.SaveChangesAsync();
        return ApiResponse<BoothRegistrationResponse>.SuccessResponse(ToResponse(registration, docs), request.Approved ? "Registration approved and booth created." : "Booth registration rejected.");
    }

    private async Task<bool> IsZoneInMarketAsync(Guid zoneId, Guid marketId) => (await _zones.GetByIdAsync(zoneId))?.NightMarketId == marketId;

    private async Task<bool> IsBoothOwnerAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            return false;

        var role = await _roles.GetByIdAsync(user.RoleId);
        return role?.RoleName == "BoothOwner";
    }

    private async Task<string?> ValidateRequestAsync(CreateBoothRegistrationRequest request)
    {
        if (await _markets.GetByIdAsync(request.RequestedNightMarketId) is null) 
            return "Night market was not found.";

        if (request.PreferredZoneId.HasValue && !(await IsZoneInMarketAsync(request.PreferredZoneId.Value, request.RequestedNightMarketId)))
            return "The selected zone does not belong to the night market.";

        if (request.PreferredLayoutNodeId.HasValue && await _nodes.GetByIdAsync(request.PreferredLayoutNodeId.Value) is null)
            return "The selected layout node was not found.";

        return null;
    }

    private List<BoothDocument> CreateDocuments(Guid registrationId, IEnumerable<BoothDocumentRequest> requests, DateTime now)
        => requests.Select(request =>
        {
            var document = _mapper.Map<BoothDocument>(request);
            document.Id = Guid.NewGuid();
            document.RegistrationId = registrationId;
            document.VerificationStatus = BoothDocumentStatus.PendingReview;
            document.CreatedAt = now;
            document.UpdatedAt = now;
            return document;
        }).ToList();

    private async Task<List<BoothRegistrationResponse>> ToResponsesAsync(IEnumerable<BoothRegistration> registrations)
    {
        var result = new List<BoothRegistrationResponse>();
        foreach (var registration in registrations) result.Add(ToResponse(registration, await _documents.FindAsync(d => d.RegistrationId == registration.Id)));
        return result;
    }

    private BoothRegistrationResponse ToResponse(BoothRegistration registration, IEnumerable<BoothDocument> documents)
    {
        var response = _mapper.Map<BoothRegistrationResponse>(registration);
        response.Documents = _mapper.Map<List<BoothDocumentResponse>>(documents);
        return response;
    }
}
