using ApplicationLayer.DTOs;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Mappings;
using ApplicationLayer.Services.Storage;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Packages;

public class PackageService : IPackageService
{
    private readonly IGenericRepository<Package> _packages;
    private readonly IPackagePriceRepository _packagePrices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<PackageService> _logger;

    public PackageService(IGenericRepository<Package> packages, IPackagePriceRepository packagePrices, IUnitOfWork unitOfWork, IMapper mapper, IFileStorageService fileStorage, ILogger<PackageService> logger)
    {
        _packages = packages;
        _packagePrices = packagePrices;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<ApiResponse<PaginationResp<PackageResponse>>> GetAllAsync(PaginationReq pagination, CancellationToken cancellationToken = default)
    {
        var page = await _packages.GetPagedAsync(
            null, pagination.Page, pagination.PageSize, package => package.CreatedAt,
            ascending: false, cancellationToken);
        return ApiResponse<PaginationResp<PackageResponse>>.SuccessResponse(
            _mapper.MapPage<Package, PackageResponse>(page, pagination));
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
        if (string.IsNullOrWhiteSpace(request.PackageName))
            throw AppException.BadRequest("Package name is required.");
        if (request.DurationDays <= 0)
            throw AppException.BadRequest("Package duration must be greater than zero.");
        if (request.Price < 0)
            throw AppException.BadRequest("Package price cannot be negative.");

        var (entitlementsJson, packageType, isFree) = PackageTemplateHelper.ResolveTemplate(request.TemplateCode);

        if (isFree && request.Price != 0)
            throw AppException.BadRequest("BOOTH_FREE package must have a price of 0.");
        if (!isFree && request.Price == 0)
            throw AppException.BadRequest("Package price must be greater than zero. Only BOOTH_FREE can have a price of 0.");

        if (request.Promotion != null)
        {
            request.Promotion.StartDate = NormalizeUtc(request.Promotion.StartDate);
            request.Promotion.EndDate = NormalizeUtc(request.Promotion.EndDate);

            var dummyPackage = new Package { Code = request.TemplateCode, Price = request.Price, DurationDays = request.DurationDays };
            await PackagePriceValidator.ValidatePackagePriceAsync(_packagePrices, dummyPackage, request.Promotion.Price, request.DurationDays, request.Promotion.StartDate, request.Promotion.EndDate);
        }

        var code = request.TemplateCode!.Trim().ToUpperInvariant();
        var name = request.PackageName.Trim();
        var nameExists = await _packages.AnyAsync(p => p.PackageName.ToLower() == name.ToLower());
        if (nameExists)
            throw AppException.Conflict("Package name already exists.", "PACKAGE_NAME_CONFLICT");

        var codeExists = await _packages.AnyAsync(p => p.Code != null && p.Code.ToLower() == code.ToLower());
        if (codeExists)
            throw AppException.Conflict("Package code already exists.", "PACKAGE_CODE_CONFLICT");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var package = new Package
            {
                Id = Guid.NewGuid(),
                PackageName = name,
                Code = code,
                Price = request.Price,
                DurationDays = request.DurationDays,
                Type = packageType,
                Description = request.Description,
                Entitlements = entitlementsJson,
                Status = request.Status,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _packages.AddAsync(package);
            await _packages.SaveChangesAsync();

            if (request.Promotion != null)
            {
                var packagePrice = new PackagePrice
                {
                    Id = Guid.NewGuid(),
                    PackageId = package.Id,
                    Price = request.Promotion.Price,
                    DurationDays = package.DurationDays,
                    StartDate = request.Promotion.StartDate,
                    EndDate = request.Promotion.EndDate,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _packagePrices.AddAsync(packagePrice);
                await _packagePrices.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return ApiResponse<PackageResponse>.SuccessResponse(_mapper.Map<PackageResponse>(package), "Package created successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<PackageResponse>> UpdateAsync(Guid packageId, UpdatePackageRequest request, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var package = await _packages.GetByIdAsync(packageId);
            if (package is null)
                throw AppException.NotFound("Package was not found.");

            if (string.IsNullOrWhiteSpace(request.PackageName))
                throw AppException.BadRequest("Package name is required.");
            if (request.DurationDays <= 0)
                throw AppException.BadRequest("Package duration must be greater than zero.");
            if (request.Price < 0)
                throw AppException.BadRequest("Package price cannot be negative.");

            var isBoothFree = package.Code == "BOOTH_FREE";
            if (isBoothFree && request.Price != 0)
                throw AppException.BadRequest("BOOTH_FREE package must have a price of 0.");
            if (!isBoothFree && request.Price == 0)
                throw AppException.BadRequest("Package price must be greater than zero. Only BOOTH_FREE can have a price of 0.");

            var name = request.PackageName.Trim();
            var nameExists = await _packages.AnyAsync(p =>
                p.PackageName.ToLower() == name.ToLower() && p.Id != packageId);
            if (nameExists)
                throw AppException.Conflict("Package name already exists.");

            if (package.DurationDays != request.DurationDays)
            {
                var hasPrices = await _packagePrices.AnyAsync(p => p.PackageId == packageId && !p.IsDeleted);
                if (hasPrices)
                    throw AppException.BadRequest("Cannot change the duration of a package that has active price records. Please create a new package instead.");
            }

            package.PackageName = name;
            package.Description = request.Description;
            package.Price = request.Price;
            package.DurationDays = request.DurationDays;
            if (isBoothFree)
            {
                if (request.Status != PackageStatus.Active)
                    throw AppException.BadRequest("The default BOOTH_FREE plan cannot be disabled or deleted.", "DEFAULT_PACKAGE_CANNOT_BE_DISABLED");
                package.Status = PackageStatus.Active;
            }
            else
            {
                package.Status = request.Status;
            }
            package.UpdatedAt = DateTime.UtcNow;

            _packages.Update(package);
            await _packages.SaveChangesAsync();

            if (request.PromotionAction.Equals("Remove", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Promotion?.Id == null)
                    throw AppException.BadRequest("Promotion ID is required to remove a promotion.", "PROMOTION_ID_REQUIRED");

                var promo = await _packagePrices.FirstOrDefaultAsync(p => p.Id == request.Promotion.Id.Value && p.PackageId == packageId);
                if (promo == null)
                    throw AppException.NotFound("Promotion was not found.", "PROMOTION_NOT_FOUND");

                promo.UpdatedAt = DateTime.UtcNow;
                _packagePrices.Delete(promo);
                await _packagePrices.SaveChangesAsync();
            }
            else if (request.PromotionAction.Equals("Upsert", StringComparison.OrdinalIgnoreCase) && request.Promotion != null)
            {
                request.Promotion.StartDate = NormalizeUtc(request.Promotion.StartDate);
                request.Promotion.EndDate = NormalizeUtc(request.Promotion.EndDate);

                await PackagePriceValidator.ValidatePackagePriceAsync(_packagePrices, package, request.Promotion.Price, request.DurationDays, request.Promotion.StartDate, request.Promotion.EndDate, request.Promotion.Id);

                if (request.Promotion.Id.HasValue)
                {
                    var existingPromo = await _packagePrices.FirstOrDefaultAsync(p => p.Id == request.Promotion.Id.Value && p.PackageId == packageId);
                    if (existingPromo != null)
                    {
                        existingPromo.Price = request.Promotion.Price;
                        existingPromo.StartDate = request.Promotion.StartDate;
                        existingPromo.EndDate = request.Promotion.EndDate;
                        existingPromo.UpdatedAt = DateTime.UtcNow;
                        _packagePrices.Update(existingPromo);
                    }
                    else
                    {
                        throw AppException.NotFound("Promotion was not found.");
                    }
                }
                else
                {
                    var packagePrice = new PackagePrice
                    {
                        Id = Guid.NewGuid(),
                        PackageId = package.Id,
                        Price = request.Promotion.Price,
                        DurationDays = package.DurationDays,
                        StartDate = request.Promotion.StartDate,
                        EndDate = request.Promotion.EndDate,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _packagePrices.AddAsync(packagePrice);
                }
                await _packagePrices.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return ApiResponse<PackageResponse>.SuccessResponse(_mapper.Map<PackageResponse>(package), "Package updated successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        if (package is null)
            throw AppException.NotFound("Package was not found.");

        if (package.Code == "BOOTH_FREE")
            throw AppException.BadRequest("The default BOOTH_FREE plan cannot be deleted.", "DEFAULT_PACKAGE_CANNOT_BE_DELETED");

        var hasSubs = await _packages.AnyAsync(p =>
            p.Id == packageId &&
            (p.BoothSubscriptions.Any() || p.MarketSubscriptions.Any()));
        if (hasSubs)
            throw AppException.BadRequest("Cannot delete a package that has subscriptions. Set it to Inactive instead.", "PACKAGE_HAS_SUBSCRIPTION_HISTORY");

        package.IsDeleted = true;
        package.UpdatedAt = DateTime.UtcNow;
        _packages.Update(package);
        await _packages.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { package.Id }, "Package deleted successfully.");
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }


    public async Task<ApiResponse<object>> StopSellingAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        if (package is null)
            throw AppException.NotFound("Package was not found.");

        if (package.Code == "BOOTH_FREE")
            throw AppException.BadRequest("The default BOOTH_FREE plan cannot be disabled.", "DEFAULT_PACKAGE_CANNOT_BE_DISABLED");

        package.Status = PackageStatus.Inactive;
        package.UpdatedAt = DateTime.UtcNow;
        _packages.Update(package);
        await _packages.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { package.Id, package.Status }, "Package is now inactive (stopped selling).");
    }

    public async Task<ApiResponse<object>> ResumeSellingAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        if (package is null)
            throw AppException.NotFound("Package was not found.");

        if (package.IsDeleted)
            throw AppException.BadRequest("Cannot resume selling a deleted package.");

        package.Status = PackageStatus.Active;
        package.UpdatedAt = DateTime.UtcNow;
        _packages.Update(package);
        await _packages.SaveChangesAsync();

        return ApiResponse<object>.SuccessResponse(new { package.Id, package.Status }, "Package is now active (resumed selling).");
    }

    public async Task<ApiResponse<List<PackageResponse>>> GetActiveByTypeAsync(PackageType type, CancellationToken cancellationToken = default)
    {
        var packages = await _packages.FindAsync(p =>
            p.Type == type &&
            p.Status == PackageStatus.Active &&
            !p.IsDeleted);

        var result = _mapper.Map<List<PackageResponse>>(packages.OrderBy(p => p.Price).ToList());
        return ApiResponse<List<PackageResponse>>.SuccessResponse(result);
    }

    public async Task<ApiResponse<PackageResponse>> GetActiveByIdAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        if (package is null || package.Status != PackageStatus.Active || package.IsDeleted)
            throw AppException.NotFound("Package was not found.");

        return ApiResponse<PackageResponse>.SuccessResponse(_mapper.Map<PackageResponse>(package));
    }

    public ApiResponse<List<PackageTemplateResponse>> GetTemplates()
    {
        return ApiResponse<List<PackageTemplateResponse>>.SuccessResponse(
            PackageTemplateHelper.Templates.ToList(),
            "Package templates retrieved successfully.");
    }

    public async Task<ApiResponse<PackageResponse>> UploadImageAsync(Guid packageId, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        if (package is null)
            throw AppException.NotFound("Package was not found.");

        var oldImageUrl = package.ImageUrl;
        var newUrl = await _fileStorage.SaveImageAsync("packages", stream, fileName, contentType, length, cancellationToken);

        try
        {
            package.ImageUrl = newUrl;
            package.UpdatedAt = DateTime.UtcNow;
            _packages.Update(package);
            await _packages.SaveChangesAsync();
        }
        catch
        {
            package.ImageUrl = oldImageUrl;
            await DeleteOldImageBestEffortAsync(newUrl, cancellationToken);
            throw;
        }

        await DeleteOldImageBestEffortAsync(oldImageUrl, cancellationToken);

        return ApiResponse<PackageResponse>.SuccessResponse(_mapper.Map<PackageResponse>(package), "Package image uploaded successfully.");
    }

    public async Task<ApiResponse<object>> DeleteImageAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(packageId);
        if (package is null)
            throw AppException.NotFound("Package was not found.");

        var oldImageUrl = package.ImageUrl;
        package.ImageUrl = null;
        package.UpdatedAt = DateTime.UtcNow;
        _packages.Update(package);
        await _packages.SaveChangesAsync();

        await DeleteOldImageBestEffortAsync(oldImageUrl, cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { package.Id }, "Package image removed successfully.");
    }

    private async Task DeleteOldImageBestEffortAsync(string? oldUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(oldUrl))
            return;

        try
        {
            await _fileStorage.DeleteImageIfManagedAsync(oldUrl, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove replaced package image {ImageUrl}.", oldUrl);
        }
    }

}
