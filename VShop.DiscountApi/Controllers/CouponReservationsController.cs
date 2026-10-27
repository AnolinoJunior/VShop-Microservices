using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VShop.DiscountApi.Models;
using VShop.DiscountApi.Services;

namespace VShop.DiscountApi.Controllers;

public record ReserveCouponRequest(Guid ReservationId, string CouponCode);

[ApiController]
[Route("api/coupon-reservations")]
[Authorize(Policy = "ApiScope")]
public sealed class CouponReservationsController(CouponReservationService service, IHttpClientFactory clients) : ControllerBase
{
    private string? UserId => User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost]
    public async Task<IActionResult> Reserve(ReserveCouponRequest request)
    {
        if (UserId is null) return Forbid();
        if (request.ReservationId == Guid.Empty || string.IsNullOrWhiteSpace(request.CouponCode) || request.CouponCode.Trim().Length > 50)
            return BadRequest("Reserva e código válidos são obrigatórios.");
        try
        {
            var existing = await service.Find(request.ReservationId, request.CouponCode.Trim(), UserId);
            if (existing is not null) return Ok(existing);
            using var call = new HttpRequestMessage(HttpMethod.Get, "api/cart/verified-subtotal");
            call.Headers.TryAddWithoutValidation("Authorization", Request.Headers.Authorization.ToString());
            using var response = await clients.CreateClient("CartApi").SendAsync(call);
            if (!response.IsSuccessStatusCode) return StatusCode(StatusCodes.Status503ServiceUnavailable, "Não foi possível validar o subtotal do carrinho.");
            var subtotal = await response.Content.ReadFromJsonAsync<decimal>();
            var result = await service.Reserve(request.ReservationId, request.CouponCode.Trim(), UserId, subtotal, DateTime.UtcNow);
            return result is null ? NotFound() : Ok(result);
        }
        catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable, "A CartApi está indisponível."); }
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
        if (result.ManagedBySaga || result.Status == CouponReservationStatus.Prepared) return Conflict(result);
        if (confirm && result.Status is CouponReservationStatus.Expired or CouponReservationStatus.Released ||
            !confirm && result.Status == CouponReservationStatus.Confirmed) return Conflict(result);
        return Ok(result);
    }
}
