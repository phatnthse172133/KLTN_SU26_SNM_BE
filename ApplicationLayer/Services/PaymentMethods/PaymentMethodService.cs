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
            // 1. Logic nghiệp vụ: Kiểm tra User
            var userExists = await _userRepo.UserExistsAsync(userId);
            if (!userExists) return ApiResponse<bool>.Failure("Không tìm thấy User trong hệ thống.", false);

            // 2. Logic nghiệp vụ: Xử lý mặc định (Default toggling)
            var oldDefault = await _paymentMethodRepo.GetDefaultMethodByUserIdAsync(userId);
            if (oldDefault != null)
            {
                oldDefault.IsDefault = false;
                _paymentMethodRepo.Update(oldDefault);
            }

            // 3. Tạo mới Payment Method
            var newMethod = new PaymentMethod
            {
                UserId = userId,
                //MethodType = dto.MethodType,
                PaymentToken = dto.TokenFromGateway,
                IsDefault = true
            };

            await _paymentMethodRepo.AddAsync(newMethod);

            // 4. Lưu tất cả thay đổi (Unit of Work)
            await _paymentMethodRepo.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true, "Liên kết phương thức thanh toán thành công!");
        }
    }
}
