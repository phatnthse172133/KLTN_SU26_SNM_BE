using DomainLayer.Entities;

namespace DomainLayer.InterfaceRepository;

public interface IOrderRepository : IGenericRepository<Order>
{
    Task<Order?> GetByCustomerAsync(Guid customerId, Guid orderId);
    Task<bool> ContainsBoothItemsAsync(Guid orderId, Guid boothId);
    Task<Order?> GetOrderByCodeAsync(long orderCode);
}
