using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.BoothMedia;

public interface IBoothMediaService
{
    Task<ApiResponse<List<BoothDocumentResponse>>> GetDocumentsAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothDocumentResponse>> UploadDocumentAsync(Guid ownerId, string? documentType, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteDocumentAsync(Guid ownerId, Guid documentId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<BoothImageResponse>>> GetImagesAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<BoothImageResponse>>> UploadImageAsync(Guid ownerId, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<BoothImageResponse>>> DeleteImageAsync(Guid ownerId, Guid imageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<BoothImageResponse>>> SetCoverImageAsync(Guid ownerId, Guid imageId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BoothLogoResponse>> UploadLogoAsync(Guid ownerId, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
}
