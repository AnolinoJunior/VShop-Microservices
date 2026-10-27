using VShop.OrderApi.Models;

namespace VShop.OrderApi.Repositories;

public interface IOrderRepository
{
    Task<OrderHeader?> GetByIdAsync(long orderId, string userId, CancellationToken cancellationToken = default);
    Task<OrderHeader?> GetByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderHeader>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<OrderHeader> AddAsync(OrderHeader order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
