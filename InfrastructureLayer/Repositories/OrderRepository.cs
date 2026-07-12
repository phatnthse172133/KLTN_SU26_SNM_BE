using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<Order?> GetByCustomerAsync(Guid customerId, Guid orderId)
        => await _dbSet.FirstOrDefaultAsync(order => order.Id == orderId && order.CustomerId == customerId);

    public async Task<bool> ContainsBoothItemsAsync(Guid orderId, Guid boothId)
        => await _context.OrderDetails
            .Include(detail => detail.FoodItem)
            .AnyAsync(detail => detail.OrderId == orderId && detail.FoodItem.BoothId == boothId);

    public async Task<Order?> GetOrderByCodeAsync(long orderCode)
        => await _dbSet.Include(order => order.Payments).FirstOrDefaultAsync(order => order.OrderCode == orderCode);
}
