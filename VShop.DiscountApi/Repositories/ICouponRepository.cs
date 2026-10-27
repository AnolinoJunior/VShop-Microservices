using VShop.DiscountApi.Models;

namespace VShop.DiscountApi.Repositories;

public interface ICouponRepository
{
    Task<IReadOnlyList<Coupon>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Coupon?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
