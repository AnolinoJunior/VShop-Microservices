using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VShop.OrderApi.Context;
using VShop.OrderApi.Models;

namespace VShop.OrderApi.Services;

public sealed class SagaConflictException : Exception;

public sealed class CheckoutSagaService(AppDbContext db, SagaRemoteClient remote, IPaymentService payment)
{
    private const string Stock = "SagaProduct";
    private const string Coupon = "SagaDiscount";
    private const string StockPath = "api/internal/saga-stock-reservations";
    private const string CouponPath = "api/internal/saga-coupon-reservations";

    private static Guid ReservationId(string kind, long orderId, long detailId = 0) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"vshop-saga:{kind}:{orderId}:{detailId}"))[..16]);

    public async Task<CheckoutSaga?> Start(long orderId, string userId, bool decline, CancellationToken ct)
    {
        var order = await db.OrderHeaders.Include(x => x.OrderDetails).SingleOrDefaultAsync(x => x.OrderHeaderId == orderId, ct);
        if (order is null || !string.Equals(order.UserId, userId, StringComparison.Ordinal)) return null;
        var saga = await db.CheckoutSagas.AsNoTracking().SingleOrDefaultAsync(x => x.OrderHeaderId == orderId, ct);
        if (saga is null)
        {
            if (order.Status != OrderStatus.Pending || order.OrderDetails.Count == 0) throw new SagaConflictException();
            saga = new CheckoutSaga
            {
                OrderHeaderId = orderId, Phase = SagaPhase.Reserving, SimulateDecline = decline,
                CouponReservationId = string.IsNullOrWhiteSpace(order.CouponCode) ? null : ReservationId("coupon", orderId),
                UpdatedAtUtc = DateTime.UtcNow,
                Items = order.OrderDetails.Select(d => new CheckoutSagaItem { OrderDetailId = d.OrderDetailId, ReservationId = ReservationId("stock", orderId, d.OrderDetailId) }).ToList()
            };
            db.CheckoutSagas.Add(saga);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                db.ChangeTracker.Clear();
                saga = await db.CheckoutSagas.AsNoTracking().SingleAsync(x => x.OrderHeaderId == orderId, ct);
            }
        }
        if (saga.SimulateDecline != decline) throw new SagaConflictException();
        db.ChangeTracker.Clear();
        await Process(orderId, ct);
        return await db.CheckoutSagas.AsNoTracking().Include(x => x.Order).SingleAsync(x => x.OrderHeaderId == orderId, ct);
    }

    public async Task Process(long orderId, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        var locked = false;
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource = @name, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @result;";
                var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = "vshop-checkout:" + orderId; command.Parameters.Add(parameter);
                locked = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) >= 0;
            }
            if (!locked) return;
            var saga = await db.CheckoutSagas.Include(s => s.Order).Include(s => s.Items).ThenInclude(i => i.OrderDetail)
                .SingleAsync(s => s.OrderHeaderId == orderId, ct);
            if (saga.Phase is SagaPhase.Completed or SagaPhase.Failed) return;
            try
            {
                saga.LastError = null;
                if (saga.Phase == SagaPhase.Reconciliation)
                {
                    saga.Phase = saga.CommitDecision ? SagaPhase.Committing : SagaPhase.Compensating;
                    await Save(saga, ct);
                }
                await Advance(saga, ct);
                await Save(saga, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                saga.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                await Save(saga, ct);
            }
        }
        finally
        {
            if (locked)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "EXEC sp_releaseapplock @Resource = @name, @LockOwner = 'Session';";
                var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = "vshop-checkout:" + orderId; command.Parameters.Add(parameter);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task Advance(CheckoutSaga saga, CancellationToken ct)
    {
        var order = saga.Order;
        var owner = new { userId = order.UserId };
        if (saga.Phase == SagaPhase.Reserving)
        {
            foreach (var item in saga.Items.OrderBy(x => x.OrderDetailId))
            {
                if (item.Reserved) continue;
                var result = await remote.Post(Stock, StockPath, new { reservationId = item.ReservationId, userId = order.UserId, productId = item.OrderDetail.ProductId, quantity = item.OrderDetail.Quantity }, ct);
                if (result.Status != HttpStatusCode.OK || Status(result.Body) != 0) { saga.Phase = SagaPhase.Compensating; saga.LastError = "Não foi possível reservar o estoque."; await Save(saga, ct); break; }
                item.Reserved = true; await Save(saga, ct);
            }
            if (saga.Phase == SagaPhase.Reserving && saga.CouponReservationId is Guid coupon && !saga.CouponReserved)
            {
                var result = await remote.Post(Coupon, CouponPath, new { reservationId = coupon, userId = order.UserId, couponCode = order.CouponCode, subtotal = order.SubtotalAmount }, ct);
                if (result.Status != HttpStatusCode.OK || Status(result.Body) != 0) { saga.Phase = SagaPhase.Compensating; saga.LastError = "Não foi possível reservar o cupom."; await Save(saga, ct); }
                else
                {
                    var discount = result.Body!.Value.GetProperty("discountAmount").GetDecimal();
                    if (discount <= 0) throw new InvalidOperationException("Desconto da reserva inválido.");
                    saga.CouponReserved = true; order.DiscountAmount = Math.Min(order.SubtotalAmount, discount); order.TotalAmount = order.SubtotalAmount - order.DiscountAmount;
                    await Save(saga, ct);
                }
            }
            if (saga.Phase == SagaPhase.Reserving) { saga.Phase = SagaPhase.Paying; await Save(saga, ct); }
        }
        if (saga.Phase == SagaPhase.Paying)
        {
            var result = await payment.AuthorizeAsync(order.OrderHeaderId, order.TotalAmount, saga.SimulateDecline ? SimulatedPaymentDecision.Decline : SimulatedPaymentDecision.Approve, ct);
            order.PaymentStatus = result == SimulatedPaymentDecision.Approve ? PaymentStatus.Approved : PaymentStatus.Declined;
            saga.Phase = result == SimulatedPaymentDecision.Approve ? SagaPhase.Preparing : SagaPhase.Compensating;
            if (saga.Phase == SagaPhase.Compensating) saga.LastError = "Pagamento demonstrativo recusado.";
            await Save(saga, ct);
        }
        if (saga.Phase == SagaPhase.Preparing)
        {
            foreach (var item in saga.Items.OrderBy(x => x.OrderDetailId))
            {
                if (item.Prepared) continue;
                var result = await remote.Post(Stock, $"{StockPath}/{item.ReservationId}/prepare", owner, ct);
                if (result.Status != HttpStatusCode.OK || Status(result.Body) != 4) { saga.Phase = SagaPhase.Compensating; saga.LastError = "Reserva de estoque expirou ou não pode ser preparada."; await Save(saga, ct); break; }
                item.Prepared = true; await Save(saga, ct);
            }
            if (saga.Phase == SagaPhase.Preparing && saga.CouponReservationId is Guid coupon && !saga.CouponPrepared)
            {
                var result = await remote.Post(Coupon, $"{CouponPath}/{coupon}/prepare", owner, ct);
                if (result.Status != HttpStatusCode.OK || Status(result.Body) != 4) { saga.Phase = SagaPhase.Compensating; saga.LastError = "Reserva de cupom expirou ou não pode ser preparada."; await Save(saga, ct); }
                else { saga.CouponPrepared = true; await Save(saga, ct); }
            }
            // Esta decisão deve ser durável antes de qualquer confirmação remota.
            if (saga.Phase == SagaPhase.Preparing) { saga.CommitDecision = true; saga.Phase = SagaPhase.Committing; await Save(saga, ct); }
        }
        if (saga.Phase == SagaPhase.Committing)
        {
            foreach (var item in saga.Items.OrderBy(x => x.OrderDetailId))
            {
                if (item.Confirmed) continue;
                var result = await remote.Post(Stock, $"{StockPath}/{item.ReservationId}/confirm", owner, ct);
                if (result.Status != HttpStatusCode.OK || Status(result.Body) != 1) { saga.Phase = SagaPhase.Reconciliation; saga.LastError = "Confirmação de estoque exige reconciliação."; await Save(saga, ct); return; }
                item.Confirmed = true; await Save(saga, ct);
            }
            if (saga.CouponReservationId is Guid coupon && !saga.CouponConfirmed)
            {
                var result = await remote.Post(Coupon, $"{CouponPath}/{coupon}/confirm", owner, ct);
                if (result.Status != HttpStatusCode.OK || Status(result.Body) != 1) { saga.Phase = SagaPhase.Reconciliation; saga.LastError = "Confirmação de cupom exige reconciliação."; await Save(saga, ct); return; }
                saga.CouponConfirmed = true; await Save(saga, ct);
            }
            order.Status = OrderStatus.Confirmed; order.ConfirmedAtUtc = DateTime.UtcNow; saga.Phase = SagaPhase.Completed; await Save(saga, ct);
        }
        if (saga.Phase == SagaPhase.Compensating)
        {
            foreach (var item in saga.Items.Where(i => i.Reserved && !i.Released))
            {
                var result = await remote.Post(Stock, $"{StockPath}/{item.ReservationId}/release", owner, ct);
                if (result.Status != HttpStatusCode.OK || Status(result.Body) is not (2 or 3)) { saga.Phase = SagaPhase.Reconciliation; saga.LastError = "Liberação de estoque exige reconciliação."; await Save(saga, ct); return; }
                item.Released = true; await Save(saga, ct);
            }
            if (saga.CouponReservationId is Guid coupon && saga.CouponReserved && !saga.CouponReleased)
            {
                var result = await remote.Post(Coupon, $"{CouponPath}/{coupon}/release", owner, ct);
                if (result.Status != HttpStatusCode.OK || Status(result.Body) is not (2 or 3)) { saga.Phase = SagaPhase.Reconciliation; saga.LastError = "Liberação de cupom exige reconciliação."; await Save(saga, ct); return; }
                saga.CouponReleased = true; await Save(saga, ct);
            }
            order.Status = OrderStatus.Failed; saga.Phase = SagaPhase.Failed; await Save(saga, ct);
        }
    }

    private static int Status(System.Text.Json.JsonElement? body) => body is { ValueKind: System.Text.Json.JsonValueKind.Object } json && json.TryGetProperty("status", out var status) ? status.GetInt32() : -1;
    private Task Save(CheckoutSaga saga, CancellationToken ct) { saga.UpdatedAtUtc = DateTime.UtcNow; return db.SaveChangesAsync(ct); }
}
