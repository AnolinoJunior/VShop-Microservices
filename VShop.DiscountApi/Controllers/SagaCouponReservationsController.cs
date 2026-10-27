using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VShop.DiscountApi.Models;
using VShop.DiscountApi.Services;

namespace VShop.DiscountApi.Controllers;

public record SagaCouponRequest(Guid ReservationId, string UserId, string CouponCode, decimal Subtotal);
public record SagaReservationOwner(string UserId);

[ApiController]
[Route("api/internal/saga-coupon-reservations")]
[Authorize(Policy = "OrderSaga")]
public sealed class SagaCouponReservationsController(CouponReservationService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Reserve(SagaCouponRequest request)
    {
        if (request.ReservationId == Guid.Empty || string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 450 || string.IsNullOrWhiteSpace(request.CouponCode) || request.CouponCode.Trim().Length > 50 || request.Subtotal < 0) return BadRequest();
        try { var result = await service.Reserve(request.ReservationId, request.CouponCode.Trim(), request.UserId, request.Subtotal, DateTime.UtcNow, saga: true); return result is null ? NotFound() : Ok(result); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("{id:guid}/prepare")]
    public async Task<IActionResult> Prepare(Guid id, SagaReservationOwner owner)
    {
        if (string.IsNullOrWhiteSpace(owner.UserId)) return BadRequest();
        var result = await service.Prepare(id, owner.UserId, DateTime.UtcNow);
        return result is null ? NotFound() : result.Status is CouponReservationStatus.Prepared or CouponReservationStatus.Confirmed ? Ok(result) : Conflict(result);
    }

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id, SagaReservationOwner owner) => Change(id, owner, true);
    [HttpPost("{id:guid}/release")]
    public Task<IActionResult> Release(Guid id, SagaReservationOwner owner) => Change(id, owner, false);
    private async Task<IActionResult> Change(Guid id, SagaReservationOwner owner, bool confirm)
    {
        if (string.IsNullOrWhiteSpace(owner.UserId)) return BadRequest();
        var result = await service.Change(id, owner.UserId, confirm, DateTime.UtcNow, saga: true);
        if (result is not null && !result.ManagedBySaga) return Conflict(result);
        return result is null ? NotFound() : (confirm && result.Status != CouponReservationStatus.Confirmed || !confirm && result.Status == CouponReservationStatus.Confirmed) ? Conflict(result) : Ok(result);
    }
}
