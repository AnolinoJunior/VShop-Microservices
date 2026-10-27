using Microsoft.EntityFrameworkCore;
using VShop.OrderApi.Context;
using VShop.OrderApi.Models;

namespace VShop.OrderApi.Repositories;

public class OrderRepository(AppDbContext context) : IOrderRepository
{
    public async Task<OrderHeader?> GetByIdAsync(long orderId, string userId, CancellationToken cancellationToken = default)
    {
        var result = await context.OrderHeaders.Include(x => x.OrderDetails)
            .SingleOrDefaultAsync(x => x.OrderHeaderId == orderId && EF.Functions.Collate(x.UserId, "Latin1_General_100_BIN2") == userId, cancellationToken);
        return result is not null && string.Equals(result.UserId, userId, StringComparison.Ordinal) ? result : null;
    }

    public async Task<OrderHeader?> GetByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var result = await context.OrderHeaders.Include(x => x.OrderDetails)
            .SingleOrDefaultAsync(x => EF.Functions.Collate(x.UserId, "Latin1_General_100_BIN2") == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);
        return result is not null && string.Equals(result.UserId, userId, StringComparison.Ordinal) ? result : null;
    }

    public async Task<IReadOnlyList<OrderHeader>> GetByUserAsync(string userId, CancellationToken cancellationToken = default) =>
        (await context.OrderHeaders.Include(x => x.OrderDetails)
            .Where(x => EF.Functions.Collate(x.UserId, "Latin1_General_100_BIN2") == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken)).Where(x => string.Equals(x.UserId, userId, StringComparison.Ordinal)).ToList();

    public async Task<OrderHeader> AddAsync(OrderHeader order, CancellationToken cancellationToken = default)
    {
        context.OrderHeaders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        return order;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);
}
