using System.Data;
using Microsoft.EntityFrameworkCore;
using Vshop.ProductApi.Context;
using Vshop.ProductApi.Models;

namespace Vshop.ProductApi.Services;

public sealed class StockReservationService(AppDbContext db)
{
    public async Task<StockReservation?> Reserve(Guid id, int productId, string userId, long quantity, DateTime now, bool saga = false)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var product = await LockProduct(productId);
        if (product is null) return null;
        await Expire(product, now);
        var existing = await db.StockReservations.FirstOrDefaultAsync(r => r.Id == id);
        if (existing is not null)
        {
            if (existing.ProductId != productId || existing.UserId != userId || existing.Quantity != quantity || existing.ManagedBySaga != saga)
                throw new InvalidOperationException("A chave de reserva já foi usada para outra solicitação.");
            await tx.CommitAsync();
            return existing;
        }
        if (product.Stock < quantity) throw new InvalidOperationException("Estoque insuficiente.");
        product.Stock -= quantity;
        var reservation = new StockReservation
        {
            Id = id, ProductId = productId, UserId = userId, Quantity = quantity,
            Status = ReservationStatus.Pending, CreatedAt = now, ExpiresAt = now.AddMinutes(15), ManagedBySaga = saga
        };
        db.StockReservations.Add(reservation);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return reservation;
    }

    public async Task<StockReservation?> Change(Guid id, string userId, bool confirm, DateTime now, bool saga = false)
    {
        var key = await db.StockReservations.AsNoTracking().Where(r => r.Id == id)
            .Select(r => new { r.ProductId, r.UserId }).FirstOrDefaultAsync();
        if (key is null || key.UserId != userId) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var product = await LockProduct(key.ProductId);
        if (product is null) return null;
        var reservation = await db.StockReservations.FirstAsync(r => r.Id == id);
        if (reservation.ManagedBySaga != saga || saga && confirm && reservation.Status == ReservationStatus.Pending)
        {
            await tx.CommitAsync();
            return reservation;
        }
        if (reservation.Status == ReservationStatus.Pending || saga && reservation.Status == ReservationStatus.Prepared)
        {
            if (reservation.Status == ReservationStatus.Pending && reservation.ExpiresAt <= now)
            {
                reservation.Status = ReservationStatus.Expired;
                product.Stock = checked(product.Stock + reservation.Quantity);
            }
            else if (confirm) reservation.Status = ReservationStatus.Confirmed;
            else
            {
                reservation.Status = ReservationStatus.Released;
                product.Stock = checked(product.Stock + reservation.Quantity);
            }
            if (reservation.Status != ReservationStatus.Pending) reservation.CompletedAt = now;
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return reservation;
    }

    public async Task<StockReservation?> Prepare(Guid id, string userId, DateTime now)
    {
        var key = await db.StockReservations.AsNoTracking().Where(r => r.Id == id)
            .Select(r => new { r.ProductId, r.UserId }).FirstOrDefaultAsync();
        if (key is null || key.UserId != userId) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var product = await LockProduct(key.ProductId);
        if (product is null) return null;
        var reservation = await db.StockReservations.FirstAsync(r => r.Id == id);
        if (!reservation.ManagedBySaga) return null;
        if (reservation.Status == ReservationStatus.Pending)
        {
            if (reservation.ExpiresAt <= now)
            {
                reservation.Status = ReservationStatus.Expired;
                reservation.CompletedAt = now;
                product.Stock = checked(product.Stock + reservation.Quantity);
            }
            else reservation.Status = ReservationStatus.Prepared;
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return reservation;
    }

    public async Task<int> ExpireBatch(DateTime now, int batchSize = 100)
    {
        var ids = await db.StockReservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt).Select(r => r.Id).Take(batchSize).ToListAsync();
        var count = 0;
        foreach (var id in ids)
        {
            var key = await db.StockReservations.AsNoTracking().Where(r => r.Id == id).Select(r => r.ProductId).FirstAsync();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var product = await LockProduct(key);
            var reservation = await db.StockReservations.FirstAsync(r => r.Id == id);
            if (reservation.Status == ReservationStatus.Pending && reservation.ExpiresAt <= now)
            {
                reservation.Status = ReservationStatus.Expired;
                reservation.CompletedAt = now;
                product!.Stock = checked(product.Stock + reservation.Quantity);
                await db.SaveChangesAsync();
                count++;
            }
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
        }
        return count;
    }

    private Task<Product?> LockProduct(int id) => db.Products
        .FromSqlInterpolated($"SELECT * FROM Products WITH (UPDLOCK, HOLDLOCK) WHERE ProductId = {id}")
        .SingleOrDefaultAsync();

    private async Task Expire(Product product, DateTime now)
    {
        var expired = await db.StockReservations.Where(r => r.ProductId == product.ProductId &&
            r.Status == ReservationStatus.Pending && r.ExpiresAt <= now).ToListAsync();
        foreach (var reservation in expired)
        {
            reservation.Status = ReservationStatus.Expired;
            reservation.CompletedAt = now;
            product.Stock = checked(product.Stock + reservation.Quantity);
        }
        await db.SaveChangesAsync();
    }
}
