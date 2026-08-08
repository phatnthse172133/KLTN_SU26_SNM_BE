using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.BoothPayOsCredentials
{
    public interface IBoothPayOsCredentialService
    {
        Task<ApiResponse<bool>> UpsertCredentialAsync(UpsertPayOsCredentialRequest request, CancellationToken cancellationToken = default);
        Task<ApiResponse<BoothPayOsStatusResponse>> GetStatusAsync(Guid boothId, CancellationToken cancellationToken = default);
    }
}
