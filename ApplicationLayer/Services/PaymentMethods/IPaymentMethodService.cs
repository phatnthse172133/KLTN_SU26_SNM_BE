using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services.PaymentMethods
{
    public interface IPaymentMethodService
    {
        Task<ApiResponse<bool>> SetupPaymentMethodAsync(SetupPaymentDto dto, Guid userId);
    }
}
