using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vshop.ProductApi.Models;
using Vshop.ProductApi.Services;

namespace Vshop.ProductApi.Controllers;

public record SagaStockRequest(Guid ReservationId, string UserId, int ProductId, long Quantity);
public record SagaReservationOwner(string UserId);

[ApiController]
[Route("api/internal/saga-stock-reservations")]
[Authorize(Policy = "OrderSaga")]
public sealed class SagaStockReservationsController(StockReservationService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Reserve(SagaStockRequest request)
    {
        if (request.ReservationId == Guid.Empty || string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 450 || request.ProductId <= 0 || request.Quantity <= 0) return BadRequest();
        try { var result = await service.Reserve(request.ReservationId, request.ProductId, request.UserId, request.Quantity, DateTime.UtcNow, saga: true); return result is null ? NotFound() : Ok(result); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("{id:guid}/prepare")]
    public async Task<IActionResult> Prepare(Guid id, SagaReservationOwner owner)
    {
        if (string.IsNullOrWhiteSpace(owner.UserId)) return BadRequest();
        var result = await service.Prepare(id, owner.UserId, DateTime.UtcNow);
        return result is null ? NotFound() : result.Status is ReservationStatus.Prepared or ReservationStatus.Confirmed ? Ok(result) : Conflict(result);
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
        return result is null ? NotFound() : (confirm && result.Status != ReservationStatus.Confirmed || !confirm && result.Status == ReservationStatus.Confirmed) ? Conflict(result) : Ok(result);
    }
}
