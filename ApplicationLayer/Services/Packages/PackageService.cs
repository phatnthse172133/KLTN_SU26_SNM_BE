using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Packages;

public class PackageService : IPackageService
{
    private readonly IGenericRepository<Package> _packages;
    private readonly IMapper _mapper;

    public PackageService(IGenericRepository<Package> packages, IMapper mapper)
    {
        _packages = packages;
        _mapper = mapper;
    }

    public async Task<ApiResponse<PaginationResp<PackageResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _packages.GetPagedAsync(null, pagination.Page, pagination.PageSize, package => package.CreatedAt, ascending: false);
        return ApiResponse<PaginationResp<PackageResponse>>.SuccessResponse(new PaginationResp<PackageResponse>
        {
            Items = _mapper.Map<List<PackageResponse>>(items),
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            Total = total
        });
    }

    public async Task<ApiResponse<PackageResponse>> GetByIdAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        return package is null
            ? throw AppException.NotFound("Package was not found.")
            : ApiResponse<PackageResponse>.SuccessResponse(_mapper.Map<PackageResponse>(package));
    }

    public async Task<ApiResponse<PackageResponse>> CreateAsync(CreatePackageRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request);

        var now = DateTime.UtcNow;
        var package = _mapper.Map<Package>(request);
        package.Id = Guid.NewGuid();
        package.CreatedAt = now;
        package.UpdatedAt = now;

        await _packages.AddAsync(package);
        await _packages.SaveChangesAsync();

        return ApiResponse<PackageResponse>.SuccessResponse(_mapper.Map<PackageResponse>(package), "Package created successfully.");
    }

    public async Task<ApiResponse<PackageResponse>> UpdateAsync(Guid packageId, UpdatePackageRequest request, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        if (package is null)
            throw AppException.NotFound("Package was not found.");

        await ValidateAsync(request, packageId);

        _mapper.Map(request, package);
        package.UpdatedAt = DateTime.UtcNow;

        _packages.Update(package);
        await _packages.SaveChangesAsync();

        return ApiResponse<PackageResponse>.SuccessResponse(_mapper.Map<PackageResponse>(package), "Package updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        if (package is null)
            throw AppException.NotFound("Package was not found.");

        _packages.Delete(package);
        await _packages.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { package.Id }, "Package deleted successfully.");
    }

    private async Task ValidateAsync(CreatePackageRequest request, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(request.PackageName))
            throw AppException.BadRequest("Package name is required.");

        if (request.Price <= 0)
            throw AppException.BadRequest("Package price must be greater than zero.");

        if (request.DurationDays <= 0)
            throw AppException.BadRequest("Package duration must be greater than zero.");

        var name = request.PackageName.Trim();
        var exists = await _packages.AnyAsync(package =>
            package.PackageName.ToLower() == name.ToLower() &&
            (!excludeId.HasValue || package.Id != excludeId.Value));

        if (exists)
            throw AppException.Conflict("Package name already exists.");
    }
}
