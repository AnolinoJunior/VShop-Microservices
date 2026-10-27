using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VShop.OrderApi.Context;
using VShop.OrderApi.DTOs;
using VShop.OrderApi.Models;

namespace VShop.OrderApi.Services;

public sealed record CartSnapshot(string? CouponCode, List<CartSnapshotItem> Items);
public sealed record CartSnapshotItem(int ProductId, string ProductName, string? ProductDescription, int Quantity, decimal UnitPrice);
public sealed record PreparationResult(OrderHeader Order, bool Created);
public sealed class PreparationConflictException : Exception;

public sealed class OrderPreparationService(AppDbContext db)
{
    public async Task<PreparationResult> Prepare(string userId, CheckoutRequestDTO input, CartSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var user = userId;
        var key = input.IdempotencyKey.Trim();
        if (string.IsNullOrWhiteSpace(user) || user.Length > 450 || string.IsNullOrWhiteSpace(key) ||
            string.IsNullOrWhiteSpace(input.FirstName) || string.IsNullOrWhiteSpace(input.LastName) ||
            string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Telephone) ||
            snapshot.Items is null || snapshot.Items.Count == 0 || snapshot.Items.Select(i => i.ProductId).Distinct().Count() != snapshot.Items.Count ||
            snapshot.Items.Any(i => i.ProductId <= 0 || i.Quantity <= 0 || i.UnitPrice < 0 ||
                decimal.Round(i.UnitPrice, 2) != i.UnitPrice || string.IsNullOrWhiteSpace(i.ProductName) ||
                i.ProductName.Length > 200 || i.ProductDescription?.Length > 1000) || snapshot.CouponCode?.Length > 50)
            throw new ArgumentException("Dados do pedido ou carrinho inválidos.");
        var details = snapshot.Items.OrderBy(i => i.ProductId).Select(i => new OrderDetail
        {
            ProductId = i.ProductId, ProductName = i.ProductName, ProductDescription = i.ProductDescription,
            Quantity = i.Quantity, UnitPrice = i.UnitPrice, LineTotal = checked(i.UnitPrice * i.Quantity)
        }).ToList();
        var subtotal = details.Sum(i => i.LineTotal);
        if (subtotal > 9999999999999999.99m || details.Any(i => i.LineTotal > 9999999999999999.99m))
            throw new ArgumentException("Valor do pedido excede o limite permitido.");
        var order = new OrderHeader
        {
            UserId = user, IdempotencyKey = key, FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(),
            Email = input.Email.Trim(), Telephone = input.Telephone.Trim(), Address = input.Address?.Trim(),
            CouponCode = snapshot.CouponCode, SubtotalAmount = subtotal, DiscountAmount = 0,
            TotalAmount = subtotal, Status = OrderStatus.Pending, PaymentStatus = PaymentStatus.Pending,
            PaymentMethod = "Demonstration", OrderDetails = details
        };
        var existing = await Find(user, key, cancellationToken);
        if (existing != null) return Match(existing, order);
        db.OrderHeaders.Add(order);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(order, true);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            existing = await Find(user, key, cancellationToken);
            if (existing is null) throw;
            return Match(existing, order);
        }
    }

    private Task<OrderHeader?> Find(string user, string key, CancellationToken cancellationToken) => db.OrderHeaders
        .AsNoTracking().Include(o => o.OrderDetails)
        .SingleOrDefaultAsync(o => o.UserId == user && o.IdempotencyKey == key, cancellationToken);

    private static PreparationResult Match(OrderHeader saved, OrderHeader proposed)
    {
        var oldItems = saved.OrderDetails.OrderBy(i => i.ProductId).ToList();
        var newItems = proposed.OrderDetails.OrderBy(i => i.ProductId).ToList();
        if (saved.UserId != proposed.UserId || saved.IdempotencyKey != proposed.IdempotencyKey ||
            saved.FirstName != proposed.FirstName || saved.LastName != proposed.LastName || saved.Email != proposed.Email ||
            saved.Telephone != proposed.Telephone || saved.Address != proposed.Address || saved.CouponCode != proposed.CouponCode ||
            oldItems.Count != newItems.Count || oldItems.Zip(newItems).Any(pair =>
                pair.First.ProductId != pair.Second.ProductId || pair.First.Quantity != pair.Second.Quantity ||
                pair.First.UnitPrice != pair.Second.UnitPrice || pair.First.ProductName != pair.Second.ProductName ||
                pair.First.ProductDescription != pair.Second.ProductDescription))
            throw new PreparationConflictException();
        return new(saved, false);
    }
}
