using System.Data;
using Microsoft.EntityFrameworkCore;
using VShop.DiscountApi.Context;
using VShop.DiscountApi.Models;

namespace VShop.DiscountApi.Services;

public sealed class CouponReservationService(AppDbContext db)
{
    public Task<CouponReservation?> Find(Guid id, string code, string userId) =>
        db.CouponReservations.AsNoTracking()
            .Join(db.Coupons, r => r.CouponId, c => c.CouponId, (r, c) => new { Reservation = r, c.CouponCode })
            .Where(x => x.Reservation.Id == id && x.Reservation.UserId == userId && x.CouponCode == code)
            .Select(x => x.Reservation).FirstOrDefaultAsync();

    public async Task<CouponReservation?> Reserve(Guid id, string code, string userId, decimal subtotal, DateTime now, bool saga = false)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var coupon = await db.Coupons.FromSqlInterpolated($"SELECT * FROM Coupons WITH (UPDLOCK, HOLDLOCK) WHERE CouponCode = {code}").SingleOrDefaultAsync();
        if (coupon is null) return null;
        var existing = await db.CouponReservations.FirstOrDefaultAsync(r => r.Id == id);
        if (existing is not null)
        {
            if (existing.CouponId != coupon.CouponId || existing.UserId != userId || existing.ManagedBySaga != saga)
                throw new InvalidOperationException("A chave de reserva já foi usada para outra solicitação.");
            await tx.CommitAsync();
            return existing;
        }
        if (!coupon.IsActive || coupon.DiscountAmount <= 0) return null;
        if (subtotal < 50m) throw new InvalidOperationException("O subtotal mínimo para utilizar cupons é de R$ 50,00.");
        var pending = await db.CouponReservations.Where(r => r.CouponId == coupon.CouponId && r.UserId == userId &&
            r.Status == CouponReservationStatus.Pending).ToListAsync();
        foreach (var reservation in pending.Where(r => r.ExpiresAt <= now))
        {
            reservation.Status = CouponReservationStatus.Expired;
            reservation.CompletedAt = now;
        }
        if (await db.CouponReservations.AnyAsync(r => r.CouponId == coupon.CouponId && r.UserId == userId &&
            (r.Status == CouponReservationStatus.Confirmed || r.Status == CouponReservationStatus.Prepared)) || pending.Any(r => r.Status == CouponReservationStatus.Pending))
            throw new InvalidOperationException("O usuário já reservou ou utilizou este cupom.");
        var result = new CouponReservation
        {
            Id = id, CouponId = coupon.CouponId, UserId = userId, Status = CouponReservationStatus.Pending,
            CreatedAt = now, ExpiresAt = now.AddMinutes(15), ManagedBySaga = saga, DiscountAmount = coupon.DiscountAmount
        };
        db.CouponReservations.Add(result);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return result;
    }

    public async Task<CouponReservation?> Change(Guid id, string userId, bool confirm, DateTime now, bool saga = false)
    {
        var key = await db.CouponReservations.AsNoTracking().Where(r => r.Id == id)
            .Select(r => new { r.CouponId, r.UserId }).FirstOrDefaultAsync();
        if (key is null || key.UserId != userId) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await db.Coupons.FromSqlInterpolated($"SELECT * FROM Coupons WITH (UPDLOCK, HOLDLOCK) WHERE CouponId = {key.CouponId}").SingleAsync();
        var reservation = await db.CouponReservations.FirstAsync(r => r.Id == id);
        if (reservation.ManagedBySaga != saga || saga && confirm && reservation.Status == CouponReservationStatus.Pending)
        {
            await tx.CommitAsync();
            return reservation;
        }
        if (reservation.Status == CouponReservationStatus.Pending || saga && reservation.Status == CouponReservationStatus.Prepared)
        {
            reservation.Status = reservation.Status == CouponReservationStatus.Pending && reservation.ExpiresAt <= now ? CouponReservationStatus.Expired :
                confirm ? CouponReservationStatus.Confirmed : CouponReservationStatus.Released;
            reservation.CompletedAt = now;
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return reservation;
    }

    public async Task<CouponReservation?> Prepare(Guid id, string userId, DateTime now)
    {
        var key = await db.CouponReservations.AsNoTracking().Where(r => r.Id == id)
            .Select(r => new { r.CouponId, r.UserId }).FirstOrDefaultAsync();
        if (key is null || key.UserId != userId) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await db.Coupons.FromSqlInterpolated($"SELECT * FROM Coupons WITH (UPDLOCK, HOLDLOCK) WHERE CouponId = {key.CouponId}").SingleAsync();
        var reservation = await db.CouponReservations.FirstAsync(r => r.Id == id);
        if (!reservation.ManagedBySaga) return null;
        if (reservation.Status == CouponReservationStatus.Pending)
        {
            reservation.Status = reservation.ExpiresAt <= now ? CouponReservationStatus.Expired : CouponReservationStatus.Prepared;
            if (reservation.Status == CouponReservationStatus.Expired) reservation.CompletedAt = now;
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return reservation;
    }

    public async Task<int> ExpireBatch(DateTime now, int batchSize = 100)
    {
        var ids = await db.CouponReservations.AsNoTracking()
            .Where(r => r.Status == CouponReservationStatus.Pending && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt).Select(r => r.Id).Take(batchSize).ToListAsync();
        var count = 0;
        foreach (var id in ids)
        {
            var key = await db.CouponReservations.AsNoTracking().Where(r => r.Id == id).Select(r => r.CouponId).FirstAsync();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await db.Coupons.FromSqlInterpolated($"SELECT * FROM Coupons WITH (UPDLOCK, HOLDLOCK) WHERE CouponId = {key}").SingleAsync();
            var reservation = await db.CouponReservations.FirstAsync(r => r.Id == id);
            if (reservation.Status == CouponReservationStatus.Pending && reservation.ExpiresAt <= now)
            {
                reservation.Status = CouponReservationStatus.Expired;
                reservation.CompletedAt = now;
                await db.SaveChangesAsync();
                count++;
            }
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
        }
        return count;
    }
}
