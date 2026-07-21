namespace ApplicationLayer.Services.Storage;

public interface IFileStorageService
{
    Task<string> SaveAvatarAsync(Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
    Task DeleteAvatarIfManagedAsync(string? avatarUrl, CancellationToken cancellationToken = default);
    Task<string> SavePaymentEvidenceAsync(Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
    Task DeletePaymentEvidenceIfManagedAsync(string? evidenceUrl, CancellationToken cancellationToken = default);
}
