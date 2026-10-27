using Microsoft.EntityFrameworkCore;
using VShop.DiscountApi.Context;
using VShop.DiscountApi.Models;

namespace VShop.DiscountApi.Repositories;

public class CouponRepository(AppDbContext context) : ICouponRepository
{
    public async Task<IReadOnlyList<Coupon>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.Coupons.AsNoTracking().OrderBy(c => c.CouponId).ToListAsync(cancellationToken);

    public Task<Coupon?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        context.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.CouponId == id, cancellationToken);

    public Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        context.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.CouponCode == code, cancellationToken);
}
