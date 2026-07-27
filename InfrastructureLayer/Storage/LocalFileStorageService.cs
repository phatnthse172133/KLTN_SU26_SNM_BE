using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace InfrastructureLayer.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly string _storageRootPath;
    private const string AvatarFolder = "avatars";
    private const string ManagedPrefix = "/uploads/avatars/";
    private const string EvidenceFolder = "payment-evidence";
    private const string EvidenceManagedPrefix = "/uploads/payment-evidence/";
    private const string ImageFolder = "images";
    private const string ImageManagedPrefix = "/uploads/images/";
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private const long MaxPdfFileSize = 10 * 1024 * 1024; // 10 MB

    // Maps detected signature type → expected MIME + extensions
    private static readonly Dictionary<string, (string Mime, HashSet<string> Extensions)> SignatureMap = new()
    {
        { "jpeg", ("image/jpeg", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg" }) },
        { "png", ("image/png", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png" }) },
        { "webp", ("image/webp", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".webp" }) },
    };

    // Magic bytes for detection
    private static readonly byte[] JpegSig = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47 };
    private static readonly byte[] RiffSig = { 0x52, 0x49, 0x46, 0x46 };
    private static readonly byte[] PdfSig = { 0x25, 0x50, 0x44, 0x46 }; // %PDF

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public LocalFileStorageService(IWebHostEnvironment env, IConfiguration configuration)
    {
        _env = env;
        var configuredPath = configuration["UploadStorage:RootPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            _storageRootPath = Path.GetFullPath(configuredPath);
            Directory.CreateDirectory(_storageRootPath);
        }
        else
        {
            _storageRootPath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        }
    }

    private string StorageRoot => _storageRootPath;

    public async Task<string> SaveAvatarAsync(Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (stream is null || length == 0)
            throw AppException.BadRequest("Avatar file is required.", "AVATAR_FILE_REQUIRED");

        if (length > MaxFileSize)
            throw AppException.PayloadTooLarge("The selected image must be 5 MB or smaller.", "AVATAR_FILE_TOO_LARGE");

        if (!AllowedMimeTypes.Contains(contentType))
            throw AppException.BadRequest("Please select a JPG, PNG, or WEBP image.", "AVATAR_FILE_TYPE_NOT_ALLOWED");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw AppException.BadRequest("Please select a JPG, PNG, or WEBP image.", "AVATAR_FILE_TYPE_NOT_ALLOWED");

        // Read magic bytes
        var headerBuffer = new byte[12];
        var bytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, 12), cancellationToken);
        stream.Position = 0;

        // Detect actual image type from signature
        string? detectedType = null;
        if (bytesRead >= 3 && headerBuffer.AsSpan(0, 3).SequenceEqual(JpegSig))
        {
            detectedType = "jpeg";
        }
        else if (bytesRead >= 4 && headerBuffer.AsSpan(0, 4).SequenceEqual(PngSig))
        {
            detectedType = "png";
        }
        else if (bytesRead >= 12 && headerBuffer.AsSpan(0, 4).SequenceEqual(RiffSig) && headerBuffer.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            detectedType = "webp";
        }

        if (detectedType is null)
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_AVATAR_FILE");

        // Cross-check: signature type must match MIME and extension
        var (expectedMime, expectedExtensions) = SignatureMap[detectedType];
        if (!string.Equals(contentType, expectedMime, StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_AVATAR_FILE");

        if (!expectedExtensions.Contains(extension))
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_AVATAR_FILE");

        // Generate random filename to prevent path traversal and collisions
        var randomFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = $"{AvatarFolder}/{randomFileName}";

        var fullPath = Path.Combine(StorageRoot, AvatarFolder);

        Directory.CreateDirectory(fullPath);

        var fullFilePath = Path.Combine(fullPath, randomFileName);
        await using (var fileStream = new FileStream(fullFilePath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        return $"/uploads/{relativePath}";
    }

    public Task DeleteAvatarIfManagedAsync(string? avatarUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return Task.CompletedTask;

        if (!avatarUrl.StartsWith(ManagedPrefix, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var fileName = Path.GetFileName(avatarUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            return Task.CompletedTask;

        var fullPath = Path.Combine(StorageRoot, AvatarFolder, fileName);

        // Ensure the resolved path is within the avatar folder (prevent path traversal)
        var fullDir = Path.GetFullPath(Path.Combine(StorageRoot, AvatarFolder));
        var fullFilePath = Path.GetFullPath(fullPath);
        if (!fullFilePath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        if (File.Exists(fullFilePath))
        {
            File.Delete(fullFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<string> SavePaymentEvidenceAsync(Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (stream is null || length == 0)
            throw AppException.BadRequest("Payment evidence file is required.", "PAYMENT_EVIDENCE_REQUIRED");

        if (length > MaxFileSize)
            throw AppException.PayloadTooLarge("The selected file must be 5 MB or smaller.", "PAYMENT_EVIDENCE_TOO_LARGE");

        if (!AllowedMimeTypes.Contains(contentType))
            throw AppException.BadRequest("Please select a JPG, PNG, or WEBP image.", "PAYMENT_EVIDENCE_TYPE_NOT_ALLOWED");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw AppException.BadRequest("Please select a JPG, PNG, or WEBP image.", "PAYMENT_EVIDENCE_TYPE_NOT_ALLOWED");

        // Read magic bytes
        var headerBuffer = new byte[12];
        var bytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, 12), cancellationToken);
        stream.Position = 0;

        string? detectedType = null;
        if (bytesRead >= 3 && headerBuffer.AsSpan(0, 3).SequenceEqual(JpegSig))
        {
            detectedType = "jpeg";
        }
        else if (bytesRead >= 4 && headerBuffer.AsSpan(0, 4).SequenceEqual(PngSig))
        {
            detectedType = "png";
        }
        else if (bytesRead >= 12 && headerBuffer.AsSpan(0, 4).SequenceEqual(RiffSig) && headerBuffer.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            detectedType = "webp";
        }

        if (detectedType is null)
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_PAYMENT_EVIDENCE_FILE");

        var (expectedMime, expectedExtensions) = SignatureMap[detectedType];
        if (!string.Equals(contentType, expectedMime, StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_PAYMENT_EVIDENCE_FILE");

        if (!expectedExtensions.Contains(extension))
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_PAYMENT_EVIDENCE_FILE");

        var randomFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = $"{EvidenceFolder}/{randomFileName}";

        var fullPath = Path.Combine(StorageRoot, EvidenceFolder);

        Directory.CreateDirectory(fullPath);

        var fullFilePath = Path.Combine(fullPath, randomFileName);
        await using (var fileStream = new FileStream(fullFilePath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        return $"/uploads/{relativePath}";
    }

    public Task DeletePaymentEvidenceIfManagedAsync(string? evidenceUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(evidenceUrl))
            return Task.CompletedTask;

        if (!evidenceUrl.StartsWith(EvidenceManagedPrefix, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var fileName = Path.GetFileName(evidenceUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            return Task.CompletedTask;

        var fullPath = Path.Combine(StorageRoot, EvidenceFolder, fileName);

        var fullDir = Path.GetFullPath(Path.Combine(StorageRoot, EvidenceFolder));
        var fullFilePath = Path.GetFullPath(fullPath);
        if (!fullFilePath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        if (File.Exists(fullFilePath))
        {
            File.Delete(fullFilePath);
        }

        return Task.CompletedTask;
    }

    public async Task<string> SaveImageAsync(string category, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (stream is null || length == 0)
            throw AppException.BadRequest("Image file is required.", "IMAGE_FILE_REQUIRED");

        if (length > MaxFileSize)
            throw AppException.PayloadTooLarge("The selected image must be 5 MB or smaller.", "IMAGE_FILE_TOO_LARGE");

        if (!AllowedMimeTypes.Contains(contentType))
            throw AppException.BadRequest("Please select a JPG, PNG, or WEBP image.", "IMAGE_FILE_TYPE_NOT_ALLOWED");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw AppException.BadRequest("Please select a JPG, PNG, or WEBP image.", "IMAGE_FILE_TYPE_NOT_ALLOWED");

        var headerBuffer = new byte[12];
        var bytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, 12), cancellationToken);
        stream.Position = 0;

        string? detectedType = null;
        if (bytesRead >= 3 && headerBuffer.AsSpan(0, 3).SequenceEqual(JpegSig))
            detectedType = "jpeg";
        else if (bytesRead >= 4 && headerBuffer.AsSpan(0, 4).SequenceEqual(PngSig))
            detectedType = "png";
        else if (bytesRead >= 12 && headerBuffer.AsSpan(0, 4).SequenceEqual(RiffSig) && headerBuffer.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            detectedType = "webp";

        if (detectedType is null)
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_IMAGE_FILE");

        var (expectedMime, expectedExtensions) = SignatureMap[detectedType];
        if (!string.Equals(contentType, expectedMime, StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_IMAGE_FILE");

        if (!expectedExtensions.Contains(extension))
            throw AppException.BadRequest("The selected file is not a valid image.", "INVALID_IMAGE_FILE");

        var safeCategory = string.IsNullOrWhiteSpace(category) ? "misc" : new string(category.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        var randomFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = $"{ImageFolder}/{safeCategory}/{randomFileName}";

        var fullPath = Path.Combine(StorageRoot, ImageFolder, safeCategory);
        Directory.CreateDirectory(fullPath);

        var fullFilePath = Path.Combine(fullPath, randomFileName);
        await using (var fileStream = new FileStream(fullFilePath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        return $"/uploads/{relativePath}";
    }

    public async Task<string> SaveDocumentAsync(string category, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (stream is null || length == 0)
            throw AppException.BadRequest("Document file is required.", "DOCUMENT_FILE_REQUIRED");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var isPdf = string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
        var isImage = AllowedMimeTypes.Contains(contentType);

        if (!isPdf && !isImage)
            throw AppException.BadRequest("Please select a JPG, PNG, WEBP or PDF file.", "DOCUMENT_FILE_TYPE_NOT_ALLOWED");

        if (isPdf && !string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("Please select a JPG, PNG, WEBP or PDF file.", "DOCUMENT_FILE_TYPE_NOT_ALLOWED");

        if (isImage && !AllowedExtensions.Contains(extension))
            throw AppException.BadRequest("Please select a JPG, PNG, WEBP or PDF file.", "DOCUMENT_FILE_TYPE_NOT_ALLOWED");

        if (isPdf && length > MaxPdfFileSize)
            throw AppException.PayloadTooLarge("The selected PDF must be 10 MB or smaller.", "DOCUMENT_FILE_TOO_LARGE");

        if (isImage && length > MaxFileSize)
            throw AppException.PayloadTooLarge("The selected image must be 5 MB or smaller.", "DOCUMENT_FILE_TOO_LARGE");

        var headerBuffer = new byte[12];
        var bytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, 12), cancellationToken);
        stream.Position = 0;

        if (isPdf)
        {
            if (bytesRead < 4 || !headerBuffer.AsSpan(0, 4).SequenceEqual(PdfSig))
                throw AppException.BadRequest("The selected file is not a valid document.", "INVALID_DOCUMENT_FILE");
        }
        else
        {
            string? detectedType = null;
            if (bytesRead >= 3 && headerBuffer.AsSpan(0, 3).SequenceEqual(JpegSig))
                detectedType = "jpeg";
            else if (bytesRead >= 4 && headerBuffer.AsSpan(0, 4).SequenceEqual(PngSig))
                detectedType = "png";
            else if (bytesRead >= 12 && headerBuffer.AsSpan(0, 4).SequenceEqual(RiffSig) && headerBuffer.AsSpan(8, 4).SequenceEqual("WEBP"u8))
                detectedType = "webp";

            if (detectedType is null)
                throw AppException.BadRequest("The selected file is not a valid document.", "INVALID_DOCUMENT_FILE");

            var (expectedMime, expectedExtensions) = SignatureMap[detectedType];
            if (!string.Equals(contentType, expectedMime, StringComparison.OrdinalIgnoreCase))
                throw AppException.BadRequest("The selected file is not a valid document.", "INVALID_DOCUMENT_FILE");

            if (!expectedExtensions.Contains(extension))
                throw AppException.BadRequest("The selected file is not a valid document.", "INVALID_DOCUMENT_FILE");
        }

        var safeCategory = string.IsNullOrWhiteSpace(category) ? "misc" : new string(category.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        var randomFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = $"{ImageFolder}/{safeCategory}/{randomFileName}";

        var fullPath = Path.Combine(StorageRoot, ImageFolder, safeCategory);
        Directory.CreateDirectory(fullPath);

        var fullFilePath = Path.Combine(fullPath, randomFileName);
        await using (var fileStream = new FileStream(fullFilePath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        return $"/uploads/{relativePath}";
    }

    public Task DeleteImageIfManagedAsync(string? imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.CompletedTask;

        if (!imageUrl.StartsWith(ImageManagedPrefix, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var relativePart = imageUrl[ImageManagedPrefix.Length..];
        var fullPath = Path.Combine(StorageRoot, ImageFolder, relativePart);

        var baseDir = Path.GetFullPath(Path.Combine(StorageRoot, ImageFolder));
        var resolvedPath = Path.GetFullPath(fullPath);
        if (!resolvedPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        if (File.Exists(resolvedPath))
            File.Delete(resolvedPath);

        return Task.CompletedTask;
    }
}
