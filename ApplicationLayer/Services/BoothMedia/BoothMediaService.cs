using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Storage;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.BoothMedia;

public class BoothMediaService : IBoothMediaService
{
    private const int MaxGalleryImages = 5;
    private const string DocumentCategory = "booth-documents";
    private const string GalleryCategory = "booth-gallery";
    private const string LogoCategory = "booth-logo";

    private readonly IBoothRepository _booths;
    private readonly IGenericRepository<BoothDocument> _documents;
    private readonly IBoothImageRepository _boothImages;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BoothMediaService> _logger;

    public BoothMediaService(
        IBoothRepository booths,
        IGenericRepository<BoothDocument> documents,
        IBoothImageRepository boothImages,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork,
        ILogger<BoothMediaService> logger)
    {
        _booths = booths;
        _documents = documents;
        _boothImages = boothImages;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private async Task<Booth> GetOwnBoothAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var booth = await _booths.GetByOwnerIdAsync(ownerId, cancellationToken);
        if (booth is null)
            throw AppException.NotFound("You do not have a booth.", "BOOTH_NOT_FOUND");
        return booth;
    }

    private async Task<List<BoothDocument>> GetBoothDocumentsAsync(Booth booth)
    {
        var docs = await _documents.FindAsync(d => d.BoothId == booth.Id);
        return docs.GroupBy(d => d.Id).Select(g => g.First()).OrderBy(d => d.CreatedAt).ToList();
    }

    private static BoothDocumentResponse MapDocument(BoothDocument document)
        => new()
        {
            Id = document.Id,
            DocumentType = document.DocumentType.ToString(),
            FileUrl = document.FileUrl,
            VerificationStatus = document.VerificationStatus.ToString(),
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };

    private static List<BoothImageResponse> MapImages(Booth booth, IEnumerable<BoothImage> images)
        => images.Select(i => new BoothImageResponse
        {
            Id = i.Id,
            BoothId = i.BoothId,
            ImageUrl = i.ImageUrl,
            DisplayOrder = i.DisplayOrder,
            IsCover = string.Equals(booth.ThumbnailUrl, i.ImageUrl, StringComparison.OrdinalIgnoreCase),
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt
        }).ToList();

    private async Task DeleteFileBestEffortAsync(string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            await _fileStorage.DeleteImageIfManagedAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove replaced booth media file {FileUrl}.", url);
        }
    }

    // ─── Documents ───

    public async Task<ApiResponse<List<BoothDocumentResponse>>> GetDocumentsAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var booth = await GetOwnBoothAsync(ownerId, cancellationToken);
        var docs = await GetBoothDocumentsAsync(booth);
        return ApiResponse<List<BoothDocumentResponse>>.SuccessResponse(
            docs.Select(MapDocument).ToList(), "Booth documents retrieved successfully.");
    }

    public async Task<ApiResponse<BoothDocumentResponse>> UploadDocumentAsync(
        Guid ownerId, string? documentType, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        var booth = await GetOwnBoothAsync(ownerId, cancellationToken);

        if (string.IsNullOrWhiteSpace(documentType)
            || !Enum.TryParse<BoothDocumentType>(documentType, true, out var parsedType)
            || !Enum.IsDefined(parsedType))
        {
            throw AppException.BadRequest(
                $"Document type must be one of: {string.Join(", ", Enum.GetNames<BoothDocumentType>())}.",
                "DOCUMENT_TYPE_INVALID");
        }

        var existing = (await GetBoothDocumentsAsync(booth))
            .Where(d => d.DocumentType == parsedType)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefault();

        var newUrl = await _fileStorage.SaveDocumentAsync(DocumentCategory, stream, fileName, contentType, length, cancellationToken);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            var document = new BoothDocument
            {
                Id = Guid.NewGuid(),
                BoothId = booth.Id,
                DocumentType = parsedType,
                DocumentUrl = newUrl,
                FileUrl = newUrl,
                VerificationStatus = BoothDocumentStatus.PendingReview,
                CreatedAt = now,
                UpdatedAt = now
            };

            try
            {
                await _documents.AddAsync(document);
                await _documents.SaveChangesAsync();
            }
            catch
            {
                await DeleteFileBestEffortAsync(newUrl, cancellationToken);
                throw;
            }

            return ApiResponse<BoothDocumentResponse>.SuccessResponse(MapDocument(document), "Document uploaded successfully.");
        }

        var oldUrl = existing.FileUrl;
        try
        {
            existing.BoothId = booth.Id;
            existing.DocumentUrl = newUrl;
            existing.FileUrl = newUrl;
            existing.VerificationStatus = BoothDocumentStatus.PendingReview;
            existing.UpdatedAt = now;
            _documents.Update(existing);
            await _documents.SaveChangesAsync();
        }
        catch
        {
            await DeleteFileBestEffortAsync(newUrl, cancellationToken);
            throw;
        }

        if (!string.Equals(oldUrl, newUrl, StringComparison.OrdinalIgnoreCase)
            && !await _documents.AnyAsync(d => d.Id != existing.Id && (d.FileUrl == oldUrl || d.DocumentUrl == oldUrl)))
        {
            await DeleteFileBestEffortAsync(oldUrl, cancellationToken);
        }

        return ApiResponse<BoothDocumentResponse>.SuccessResponse(
            MapDocument(existing), "Document replaced successfully. It has been sent back for review.");
    }

    public async Task<ApiResponse<object>> DeleteDocumentAsync(Guid ownerId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var booth = await GetOwnBoothAsync(ownerId, cancellationToken);

        var document = await _documents.FirstOrDefaultAsync(d => d.Id == documentId && d.BoothId == booth.Id);

        if (document is null)
            throw AppException.NotFound("Document was not found.", "DOCUMENT_NOT_FOUND");

        if (document.VerificationStatus == BoothDocumentStatus.Verified)
            throw AppException.Conflict(
                "A verified document cannot be deleted. Upload a replacement to send it back for review instead.",
                "DOCUMENT_VERIFIED_LOCKED");

        var fileUrl = document.FileUrl;
        var documentUrl = document.DocumentUrl;
        _documents.Delete(document);
        await _documents.SaveChangesAsync();

        if (!await _documents.AnyAsync(d => d.FileUrl == fileUrl || d.DocumentUrl == fileUrl))
        {
            await DeleteFileBestEffortAsync(fileUrl, cancellationToken);
        }

        if (!string.Equals(documentUrl, fileUrl, StringComparison.OrdinalIgnoreCase)
            && !await _documents.AnyAsync(d => d.FileUrl == documentUrl || d.DocumentUrl == documentUrl))
        {
            await DeleteFileBestEffortAsync(documentUrl, cancellationToken);
        }

        return ApiResponse<object>.SuccessResponse(new { document.Id }, "Document deleted successfully.");
    }

    // ─── Gallery ───

    private async Task<List<BoothImage>> GetGalleryAsync(Guid boothId)
    {
        var images = await _boothImages.FindAsync(i => i.BoothId == boothId);
        return images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.CreatedAt).ToList();
    }

    public async Task<ApiResponse<List<BoothImageResponse>>> GetImagesAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var booth = await GetOwnBoothAsync(ownerId, cancellationToken);
        var images = await GetGalleryAsync(booth.Id);
        return ApiResponse<List<BoothImageResponse>>.SuccessResponse(MapImages(booth, images), "Booth images retrieved successfully.");
    }

    public async Task<ApiResponse<List<BoothImageResponse>>> UploadImageAsync(
        Guid ownerId, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        var booth = await GetOwnBoothAsync(ownerId, cancellationToken);

        if (stream is null || length == 0)
            throw AppException.BadRequest("Image file is required.", "IMAGE_FILE_REQUIRED");

        string? savedUrl = null;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _boothImages.AcquireGalleryLockAsync(booth.Id, cancellationToken);

            var images = await GetGalleryAsync(booth.Id);
            if (images.Count >= MaxGalleryImages)
                throw AppException.BadRequest("You can upload up to 5 images.", "BOOTH_GALLERY_LIMIT_REACHED");

            var url = await _fileStorage.SaveImageAsync(GalleryCategory, stream, fileName, contentType, length, cancellationToken);
            savedUrl = url;

            var now = DateTime.UtcNow;
            var newImage = new BoothImage
            {
                Id = Guid.NewGuid(),
                BoothId = booth.Id,
                ImageUrl = url,
                DisplayOrder = images.Count,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _boothImages.AddAsync(newImage);
            await _boothImages.SaveChangesAsync();

            if (images.Count == 0 && string.IsNullOrWhiteSpace(booth.ThumbnailUrl))
            {
                booth.ThumbnailUrl = url;
                booth.UpdatedAt = now;
                _booths.Update(booth);
                await _booths.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            images.Add(newImage);
            return ApiResponse<List<BoothImageResponse>>.SuccessResponse(MapImages(booth, images), "Image uploaded successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            await DeleteFileBestEffortAsync(savedUrl, cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponse<List<BoothImageResponse>>> DeleteImageAsync(
        Guid ownerId, Guid imageId, CancellationToken cancellationToken = default)
    {
        var booth = await GetOwnBoothAsync(ownerId, cancellationToken);

        string deletedUrl;
        List<BoothImage> remaining;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _boothImages.AcquireGalleryLockAsync(booth.Id, cancellationToken);

            var images = await GetGalleryAsync(booth.Id);
            var target = images.FirstOrDefault(i => i.Id == imageId);
            if (target is null)
                throw AppException.NotFound("Image was not found.", "BOOTH_IMAGE_NOT_FOUND");

            var now = DateTime.UtcNow;
            var wasCover = string.Equals(booth.ThumbnailUrl, target.ImageUrl, StringComparison.OrdinalIgnoreCase);
            deletedUrl = target.ImageUrl;

            _boothImages.Delete(target);

            remaining = images.Where(i => i.Id != imageId).OrderBy(i => i.DisplayOrder).ToList();
            for (var i = 0; i < remaining.Count; i++)
            {
                remaining[i].DisplayOrder = i;
                remaining[i].UpdatedAt = now;
            }
            if (remaining.Count > 0)
            {
                _boothImages.UpdateRange(remaining);
            }
            await _boothImages.SaveChangesAsync();

            if (wasCover)
            {
                booth.ThumbnailUrl = remaining.Count > 0 ? remaining[0].ImageUrl : null;
                booth.UpdatedAt = now;
                _booths.Update(booth);
                await _booths.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var stillReferenced = string.Equals(booth.ThumbnailUrl, deletedUrl, StringComparison.OrdinalIgnoreCase)
            || await _boothImages.AnyAsync(i => i.ImageUrl == deletedUrl);
        if (!stillReferenced)
        {
            await DeleteFileBestEffortAsync(deletedUrl, cancellationToken);
        }

        return ApiResponse<List<BoothImageResponse>>.SuccessResponse(MapImages(booth, remaining), "Image deleted successfully.");
    }

    public async Task<ApiResponse<List<BoothImageResponse>>> SetCoverImageAsync(
        Guid ownerId, Guid imageId, CancellationToken cancellationToken = default)
    {
        var booth = await GetOwnBoothAsync(ownerId, cancellationToken);

        List<BoothImage> images;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _boothImages.AcquireGalleryLockAsync(booth.Id, cancellationToken);

            images = await GetGalleryAsync(booth.Id);
            var target = images.FirstOrDefault(i => i.Id == imageId);
            if (target is null)
                throw AppException.NotFound("Image was not found.", "BOOTH_IMAGE_NOT_FOUND");

            booth.ThumbnailUrl = target.ImageUrl;
            booth.UpdatedAt = DateTime.UtcNow;
            _booths.Update(booth);
            await _booths.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return ApiResponse<List<BoothImageResponse>>.SuccessResponse(MapImages(booth, images), "Cover image set successfully.");
    }

    // ─── Logo ───

    public async Task<ApiResponse<BoothLogoResponse>> UploadLogoAsync(
        Guid ownerId, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        var booth = await GetOwnBoothAsync(ownerId, cancellationToken);

        var oldUrl = booth.LogoUrl;
        var newUrl = await _fileStorage.SaveImageAsync(LogoCategory, stream, fileName, contentType, length, cancellationToken);

        try
        {
            booth.LogoUrl = newUrl;
            booth.UpdatedAt = DateTime.UtcNow;
            _booths.Update(booth);
            await _booths.SaveChangesAsync();
        }
        catch
        {
            booth.LogoUrl = oldUrl;
            await DeleteFileBestEffortAsync(newUrl, cancellationToken);
            throw;
        }

        await DeleteFileBestEffortAsync(oldUrl, cancellationToken);

        return ApiResponse<BoothLogoResponse>.SuccessResponse(
            new BoothLogoResponse { BoothId = booth.Id, LogoUrl = booth.LogoUrl },
            "Booth logo uploaded successfully.");
    }
}
