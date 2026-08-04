using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepositories;
using DomainLayer.InterfaceRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.PaymentMethods
{
    public class PaymentMethodService : IPaymentMethodService
    {
        private readonly IPaymentMethodRepository _paymentMethodRepo;
        private readonly IUserRepository _userRepo;

        public PaymentMethodService(IPaymentMethodRepository paymentMethodRepo, IUserRepository userRepo)
        {
            _paymentMethodRepo = paymentMethodRepo;
            _userRepo = userRepo;
        }

        public async Task<ApiResponse<bool>> SetupPaymentMethodAsync(SetupPaymentDto dto, Guid userId)
        {
            // 1. Logic nghiá»‡p vá»¥: Kiá»ƒm tra User
            var userExists = await _userRepo.UserExistsAsync(userId);
            if (!userExists) return ApiResponse<bool>.Failure("KhÃ´ng tÃ¬m tháº¥y User trong há»‡ thá»‘ng.", data: false);

            // 2. Logic nghiá»‡p vá»¥: Xá»­ lÃ½ máº·c Ä‘á»‹nh (Default toggling)
            var oldDefault = await _paymentMethodRepo.GetDefaultMethodByUserIdAsync(userId);
            if (oldDefault != null)
            {
                oldDefault.IsDefault = false;
                _paymentMethodRepo.Update(oldDefault);
            }

            // 3. Táº¡o má»›i Payment Method
            var newMethod = new PaymentMethod
            {
                UserId = userId,
                MethodType = dto.MethodType,
                PaymentToken = dto.TokenFromGateway,
                IsDefault = true
            };

            await _paymentMethodRepo.AddAsync(newMethod);

            // 4. LÆ°u táº¥t cáº£ thay Ä‘á»•i (Unit of Work)
            await _paymentMethodRepo.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true, "LiÃªn káº¿t phÆ°Æ¡ng thá»©c thanh toÃ¡n thÃ nh cÃ´ng!");
        }
    }
}
