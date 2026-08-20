using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Storage;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace InfrastructureLayer.Storage;

public sealed class CloudinaryFileStorageService : IFileStorageService
{
    private const long MaxImageSize = 5 * 1024 * 1024;
    private const long MaxPdfSize = 10 * 1024 * 1024;
    private readonly Cloudinary _cloudinary;
    private readonly string _rootFolder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri _bridgeBaseUri;

    public CloudinaryFileStorageService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloudName)
            || string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(apiSecret))
            throw new InvalidOperationException(
                "Cloudinary storage is enabled but Cloudinary credentials are incomplete.");

        _cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret))
        {
            Api = { Secure = true }
        };
        _rootFolder = NormalizeCategory(configuration["Cloudinary:RootFolder"] ?? "smart-night-market");
        var bridgeUrl = configuration["Cloudinary:BridgeUrl"] ?? "http://127.0.0.1:5290/";
        if (!Uri.TryCreate(bridgeUrl, UriKind.Absolute, out var parsedBridgeUrl))
            throw new InvalidOperationException("Cloudinary bridge URL is invalid.");
        _bridgeBaseUri = parsedBridgeUrl;
    }

    public Task<string> SaveAvatarAsync(
        Stream stream, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default)
        => UploadImageAsync("avatars", stream, fileName, contentType, length, cancellationToken);

    public Task<string> SavePaymentEvidenceAsync(
        Stream stream, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default)
        => UploadImageAsync("payment-evidence", stream, fileName, contentType, length, cancellationToken);

    public Task<string> SaveImageAsync(
        string category, Stream stream, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default)
        => UploadImageAsync(category, stream, fileName, contentType, length, cancellationToken);

    public async Task<string> SaveDocumentAsync(
        string category, Stream stream, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            ValidateRequired(stream, length, MaxPdfSize, "DOCUMENT");
            if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw AppException.BadRequest(
                    "Please select a JPG, PNG, WEBP or PDF file.",
                    "DOCUMENT_FILE_TYPE_NOT_ALLOWED");

            await ValidateSignatureAsync(stream, "pdf", cancellationToken);
            var publicId = BuildPublicId(category);
            var result = await _cloudinary.UploadAsync(new RawUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = publicId,
                Overwrite = false
            }, null, cancellationToken);
            EnsureUploadSucceeded(result);
            return result.SecureUrl?.AbsoluteUri
                ?? throw AppException.BadRequest("The document could not be uploaded.", "DOCUMENT_UPLOAD_FAILED");
        }

        return await UploadImageAsync(category, stream, fileName, contentType, length, cancellationToken);
    }

    public Task DeleteAvatarIfManagedAsync(
        string? avatarUrl, CancellationToken cancellationToken = default)
        => DeleteManagedAsync(avatarUrl, cancellationToken);

    public Task DeletePaymentEvidenceIfManagedAsync(
        string? evidenceUrl, CancellationToken cancellationToken = default)
        => DeleteManagedAsync(evidenceUrl, cancellationToken);

    public Task DeleteImageIfManagedAsync(
        string? imageUrl, CancellationToken cancellationToken = default)
        => DeleteManagedAsync(imageUrl, cancellationToken);

    private async Task<string> UploadImageAsync(
        string category, Stream stream, string fileName, string contentType, long length,
        CancellationToken cancellationToken)
    {
        ValidateRequired(stream, length, MaxImageSize, "IMAGE");
        var kind = GetImageKind(fileName, contentType);

        // Buffer once because a failed SDK upload may dispose the multipart stream.
        await using var payload = new MemoryStream();
        await stream.CopyToAsync(payload, cancellationToken);
        var bytes = payload.ToArray();
        await using var signatureStream = new MemoryStream(bytes, writable: false);
        await ValidateSignatureAsync(signatureStream, kind, cancellationToken);

        try
        {
            await using var cloudinaryStream = new MemoryStream(bytes, writable: false);
            var result = await _cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(fileName, cloudinaryStream),
                PublicId = BuildPublicId(category),
                Overwrite = false,
                UseFilename = false,
                UniqueFilename = true
            }, cancellationToken);
            EnsureUploadSucceeded(result);
            return result.SecureUrl?.AbsoluteUri
                ?? throw AppException.BadRequest("The image could not be uploaded.", "IMAGE_UPLOAD_FAILED");
        }
        catch (HttpRequestException)
        {
            await using var bridgeStream = new MemoryStream(bytes, writable: false);
            return await UploadViaBridgeAsync(category, bridgeStream, fileName, contentType, "image", cancellationToken);
        }
    }

    private async Task<string> UploadViaBridgeAsync(
        string category, Stream stream, string fileName, string contentType, string resourceType,
        CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
            throw AppException.ServiceUnavailable(
                "Image upload is temporarily unavailable. Please try again.",
                "IMAGE_STORAGE_UNAVAILABLE");

        stream.Position = 0;
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var client = _httpClientFactory.CreateClient("CloudinaryBridge");
        client.BaseAddress = _bridgeBaseUri;
        using var response = await client.PostAsJsonAsync("upload", new
        {
            category,
            fileName,
            contentType,
            resourceType,
            fileBase64 = Convert.ToBase64String(buffer.ToArray())
        }, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw AppException.ServiceUnavailable(
                "Image upload is temporarily unavailable. Please try again.",
                "IMAGE_STORAGE_UNAVAILABLE");

        var result = await response.Content.ReadFromJsonAsync<BridgeUploadResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(result?.SecureUrl))
            throw AppException.ServiceUnavailable(
                "Image upload is temporarily unavailable. Please try again.",
                "IMAGE_STORAGE_UNAVAILABLE");

        return result.SecureUrl;
    }

    private sealed record BridgeUploadResponse(string? SecureUrl);

    private async Task DeleteManagedAsync(string? url, CancellationToken cancellationToken)
    {
        var publicId = TryGetManagedPublicId(url);
        if (publicId is null)
            return;

        var resourceType = url!.Contains("/raw/upload/", StringComparison.OrdinalIgnoreCase)
            ? ResourceType.Raw
            : ResourceType.Image;
        cancellationToken.ThrowIfCancellationRequested();
        await _cloudinary.DestroyAsync(new DeletionParams(publicId)
        {
            ResourceType = resourceType,
            Invalidate = true
        });
    }

    private string BuildPublicId(string category)
        => $"{_rootFolder}/{NormalizeCategory(category)}/{Guid.NewGuid():N}";

    private string? TryGetManagedPublicId(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Host.EndsWith("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var marker = "/upload/";
        var markerIndex = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        var remainder = Uri.UnescapeDataString(uri.AbsolutePath[(markerIndex + marker.Length)..]);
        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var versionIndex = Array.FindIndex(segments, value =>
            value.Length > 1 && value[0] == 'v' && value[1..].All(char.IsDigit));
        var publicSegments = versionIndex >= 0 ? segments[(versionIndex + 1)..] : segments;
        if (publicSegments.Length == 0)
            return null;

        var publicId = string.Join('/', publicSegments);
        var extension = Path.GetExtension(publicId);
        if (!string.IsNullOrWhiteSpace(extension))
            publicId = publicId[..^extension.Length];
        return publicId.StartsWith($"{_rootFolder}/", StringComparison.Ordinal) ? publicId : null;
    }

    private static void ValidateRequired(Stream stream, long length, long maxLength, string prefix)
    {
        if (stream is null || length <= 0)
            throw AppException.BadRequest("A file is required.", $"{prefix}_FILE_REQUIRED");
        if (length > maxLength)
            throw AppException.PayloadTooLarge(
                $"The selected file must be {maxLength / 1024 / 1024} MB or smaller.",
                $"{prefix}_FILE_TOO_LARGE");
        if (!stream.CanSeek)
            throw AppException.BadRequest("The selected file could not be read.", $"INVALID_{prefix}_FILE");
    }

    private static string GetImageKind(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return (contentType.ToLowerInvariant(), extension) switch
        {
            ("image/jpeg", ".jpg" or ".jpeg") => "jpeg",
            ("image/png", ".png") => "png",
            ("image/webp", ".webp") => "webp",
            _ => throw AppException.BadRequest(
                "Please select a JPG, PNG, or WEBP image.",
                "IMAGE_FILE_TYPE_NOT_ALLOWED")
        };
    }

    private static async Task ValidateSignatureAsync(
        Stream stream, string expectedKind, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var count = await stream.ReadAsync(header.AsMemory(), cancellationToken);
        stream.Position = 0;
        var valid = expectedKind switch
        {
            "jpeg" => count >= 3 && header.AsSpan(0, 3).SequenceEqual(new byte[] { 0xff, 0xd8, 0xff }),
            "png" => count >= 4 && header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47 }),
            "webp" => count >= 12
                      && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                      && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            "pdf" => count >= 4 && header.AsSpan(0, 4).SequenceEqual("%PDF"u8),
            _ => false
        };
        if (!valid)
            throw AppException.BadRequest("The selected file is not valid.", "INVALID_FILE");
    }

    private static void EnsureUploadSucceeded(UploadResult result)
    {
        if (result.Error is not null)
            throw AppException.BadRequest("The file could not be uploaded.", "FILE_UPLOAD_FAILED");
    }

    private static string NormalizeCategory(string value)
    {
        var normalized = new string(value
            .ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "misc" : normalized;
    }
}
