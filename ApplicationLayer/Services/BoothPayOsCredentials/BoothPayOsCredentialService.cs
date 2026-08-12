using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.EncryptionServices;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.BoothPayOsCredentials
{
    public class BoothPayOsCredentialService : IBoothPayOsCredentialService
    {
        private readonly IBoothPayOsCredentialRepository _repository;
        private readonly IEncryptionService _encryptionService; // Service mã hóa AES 

        public BoothPayOsCredentialService(
            IBoothPayOsCredentialRepository repository,
            IEncryptionService encryptionService)
        {
            _repository = repository;
            _encryptionService = encryptionService;
        }

        public async Task<ApiResponse<bool>> UpsertCredentialAsync(
            UpsertPayOsCredentialRequest request,
            CancellationToken cancellationToken = default)
        {
            var payInValues = new[] { request.ClientId, request.ApiKey, request.ChecksumKey };
            if (payInValues.Any(value => !string.IsNullOrWhiteSpace(value))
                && payInValues.Any(string.IsNullOrWhiteSpace))
            {
                throw AppException.BadRequest(
                    "Enter the client ID, API key, and checksum key together.",
                    "PAYMENT_CREDENTIALS_INCOMPLETE");
            }

            var payoutValues = new[] { request.PayoutClientId, request.PayoutApiKey, request.PayoutChecksumKey };
            if (payoutValues.Any(value => !string.IsNullOrWhiteSpace(value))
                && payoutValues.Any(string.IsNullOrWhiteSpace))
            {
                throw AppException.BadRequest(
                    "Enter all payout credential fields together.",
                    "PAYOUT_CREDENTIALS_INCOMPLETE");
            }

            var credential = await _repository.GetByBoothIdAsync(request.BoothId, cancellationToken);
            bool isNew = false;

            // Nếu chưa có record thì tạo mới
            if (credential is null)
            {
                isNew = true;
                credential = new BoothPayOsCredential
                {
                    Id = Guid.NewGuid(),
                    BoothId = request.BoothId,
                    CreatedAt = DateTime.UtcNow
                };
            }

            // 1. Cập nhật PayIn nếu FE có truyền lên (không rỗng)
            if (!string.IsNullOrWhiteSpace(request.ClientId))
                credential.EncryptedClientId = _encryptionService.Encrypt(request.ClientId.Trim());

            if (!string.IsNullOrWhiteSpace(request.ApiKey))
                credential.EncryptedApiKey = _encryptionService.Encrypt(request.ApiKey.Trim());

            if (!string.IsNullOrWhiteSpace(request.ChecksumKey))
                credential.EncryptedChecksumKey = _encryptionService.Encrypt(request.ChecksumKey.Trim());

            // 2. Cập nhật PayOut nếu FE có truyền lên. NẾU FE ĐỂ TRỐNG -> GIỮ NGUYÊN KEY CŨ TRONG DB!
            if (!string.IsNullOrWhiteSpace(request.PayoutClientId))
                credential.EncryptedPayoutClientId = _encryptionService.Encrypt(request.PayoutClientId.Trim());

            if (!string.IsNullOrWhiteSpace(request.PayoutApiKey))
                credential.EncryptedPayoutApiKey = _encryptionService.Encrypt(request.PayoutApiKey.Trim());

            if (!string.IsNullOrWhiteSpace(request.PayoutChecksumKey))
                credential.EncryptedPayoutChecksumKey = _encryptionService.Encrypt(request.PayoutChecksumKey.Trim());

            credential.UpdatedAt = DateTime.UtcNow;

            // 3. Save Changes
            if (isNew)
                await _repository.AddAsync(credential, cancellationToken);
            else
                await _repository.UpdateAsync(credential, cancellationToken);

            return ApiResponse<bool>.SuccessResponse(true, "Cấu hình tài khoản PayOS thành công.");
        }

        public async Task<ApiResponse<BoothPayOsStatusResponse>> GetStatusAsync(
            Guid boothId,
            CancellationToken cancellationToken = default)
        {
            var credential = await _repository.GetByBoothIdAsync(boothId, cancellationToken);

            // Kiểm tra đủ bộ 3 key cho PayIn
            bool isPayInConfigured = !string.IsNullOrWhiteSpace(credential?.EncryptedClientId)
                                  && !string.IsNullOrWhiteSpace(credential?.EncryptedApiKey)
                                  && !string.IsNullOrWhiteSpace(credential?.EncryptedChecksumKey);

            // Kiểm tra đủ bộ 3 key cho PayOut
            bool isPayOutConfigured = !string.IsNullOrWhiteSpace(credential?.EncryptedPayoutClientId)
                                   && !string.IsNullOrWhiteSpace(credential?.EncryptedPayoutApiKey)
                                   && !string.IsNullOrWhiteSpace(credential?.EncryptedPayoutChecksumKey);

            return ApiResponse<BoothPayOsStatusResponse>.SuccessResponse(new BoothPayOsStatusResponse
            {
                IsPayInConfigured = isPayInConfigured,
                IsPayOutConfigured = isPayOutConfigured,
                UpdatedAt = credential?.UpdatedAt ?? credential?.CreatedAt
            });
        }

        /// <summary>
        /// Hàm Helper dùng chung để che mờ Client ID (VD: "CLIENT123456" -> "********3456")
        /// </summary>
        private static string MaskClientId(string? rawClientId)
        {
            if (string.IsNullOrWhiteSpace(rawClientId))
                return string.Empty;

            return rawClientId.Length > 4
                ? string.Concat(new string('*', rawClientId.Length - 4), rawClientId[^4..])
                : rawClientId;
        }
    }
}
