using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vshop.ProductApi.Services;

namespace Vshop.ProductApi.Controllers;

public record ReserveStockRequest(Guid ReservationId, int ProductId, long Quantity);

[ApiController]
[Route("api/stock-reservations")]
[Authorize(Policy = "ApiScope")]
public sealed class StockReservationsController(StockReservationService service) : ControllerBase
{
    private string? UserId => User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost]
    public async Task<IActionResult> Reserve(ReserveStockRequest request)
    {
        if (UserId is null) return Forbid();
        if (request.ReservationId == Guid.Empty || request.ProductId <= 0 || request.Quantity <= 0)
            return BadRequest("Reserva, produto e quantidade válidos são obrigatórios.");
        try
        {
            var result = await service.Reserve(request.ReservationId, request.ProductId, UserId, request.Quantity, DateTime.UtcNow);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id) => Change(id, true);

    [HttpPost("{id:guid}/release")]
    public Task<IActionResult> Release(Guid id) => Change(id, false);

    private async Task<IActionResult> Change(Guid id, bool confirm)
    {
        if (UserId is null) return Forbid();
        var result = await service.Change(id, UserId, confirm, DateTime.UtcNow);
        if (result is null) return NotFound();
        if (result.ManagedBySaga || result.Status == Models.ReservationStatus.Prepared) return Conflict(result);
        if (result.Status is Models.ReservationStatus.Expired or Models.ReservationStatus.Released && confirm ||
            result.Status == Models.ReservationStatus.Confirmed && !confirm) return Conflict(result);
        return Ok(result);
    }
}
