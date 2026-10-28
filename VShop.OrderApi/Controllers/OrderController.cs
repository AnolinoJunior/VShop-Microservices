
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VShop.OrderApi.DTOs;
using VShop.OrderApi.Models;
using VShop.OrderApi.Repositories;
using VShop.OrderApi.Services;

namespace VShop.OrderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ApiScope")]
public class OrderController(
    IOrderRepository repository,
    OrderPreparationService preparation,
    IHttpClientFactory clients,
    CheckoutSagaService sagaService) : ControllerBase
{
    // ==========================================
    // CHECKOUT - EXECUTA A SAGA
    // ==========================================

    [HttpPost("{id:long}/checkout")]
    public async Task<IActionResult> Checkout(
        long id,
        [FromBody] CheckoutSagaRequest input,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        try
        {
            var saga = await sagaService.Start(
                id,
                userId,
                input.SimulateDecline,
                cancellationToken);

            if (saga is null)
                return NotFound();

            var result = new
            {
                orderId = id,
                phase = saga.Phase.ToString(),
                orderStatus = saga.Order.Status.ToString(),
                paymentStatus = saga.Order.PaymentStatus.ToString(),
                saga.LastError
            };

            return saga.Phase is SagaPhase.Completed or SagaPhase.Failed
                ? Ok(result)
                : StatusCode(StatusCodes.Status202Accepted, result);
        }
        catch (SagaConflictException)
        {
            return Conflict(
                "A tentativa já existe com outra decisão de pagamento " +
                "ou o pedido não está pendente.");
        }
    }

    // ==========================================
    // PREPARAÇÃO DO PEDIDO
    // ==========================================

    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(
        [FromBody] CheckoutRequestDTO input,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        if (input is null ||
            string.IsNullOrWhiteSpace(input.IdempotencyKey) ||
            input.IdempotencyKey.Length > 100)
        {
            return BadRequest("Chave de idempotência inválida.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "api/cart/order-snapshot");

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            Request.Headers.Authorization.ToString());

        try
        {
            using var response = await clients
                .CreateClient("CartApi")
                .SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var existing = await repository
                    .GetByIdempotencyKeyAsync(
                        userId,
                        input.IdempotencyKey.Trim(),
                        cancellationToken);

                return existing is null
                    ? BadRequest("Carrinho vazio ou inexistente.")
                    : Conflict(
                        "A chave já foi usada e o carrinho atual " +
                        "está vazio ou inexistente.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Não foi possível consultar o carrinho autenticado.");
            }

            var snapshot = await response.Content
                .ReadFromJsonAsync<CartSnapshot>(
                    cancellationToken: cancellationToken);

            if (snapshot is null)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Snapshot de carrinho inválido.");
            }

            var result = await preparation.Prepare(
                userId,
                input,
                snapshot,
                cancellationToken);

            var body = Map(result.Order);

            return result.Created
                ? StatusCode(StatusCodes.Status201Created, body)
                : Ok(body);
        }
        catch (PreparationConflictException)
        {
            return Conflict(
                "A chave de idempotência já foi usada com outros dados.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OverflowException)
        {
            return BadRequest(
                "Valor do pedido excede o limite permitido.");
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "A CartiApi está indisponível.");
        }
    }

    // ==========================================
    // LISTAR PEDIDOS DO USUÁRIO
    // GET /api/Order
    // ==========================================

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDTO>>> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var orders = await repository.GetByUserAsync(
            userId,
            cancellationToken);

        return Ok(orders.Select(Map).ToList());
    }

    // ==========================================
    // RECUPERAR PEDIDO PELA CHAVE DE IDEMPOTÊNCIA
    // GET /api/Order/by-idempotency-key/{key}
    // ==========================================

    [HttpGet("by-idempotency-key/{key}")]
    public async Task<ActionResult<OrderDTO>> GetByIdempotencyKey(
        string key,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(key) ||
            key.Length > 100)
        {
            return BadRequest(
                "Chave de idempotência inválida.");
        }

        var order = await repository.GetByIdempotencyKeyAsync(
            userId,
            key,
            cancellationToken);

        if (order is null)
        {
            return NotFound(
                "Nenhum pedido encontrado para essa chave.");
        }

        return Ok(Map(order));
    }

    // ==========================================
    // CONSULTAR PEDIDO PELO ID
    // GET /api/Order/{id}
    // ==========================================

    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDTO>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var order = await repository.GetByIdAsync(
            id,
            userId,
            cancellationToken);

        return order is null
            ? NotFound()
            : Ok(Map(order));
    }

    // ==========================================
    // IDENTIFICAÇÃO DO USUÁRIO AUTENTICADO
    // ==========================================

    private string? GetUserId()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    // ==========================================
    // MAPEAMENTO: ENTITY -> DTO
    // ==========================================

    private static OrderDTO Map(OrderHeader order) => new()
    {
        OrderHeaderId = order.OrderHeaderId,
        UserId = order.UserId,

        FirstName = order.FirstName,
        LastName = order.LastName,
        Email = order.Email,
        Telephone = order.Telephone,
        Address = order.Address,

        CouponCode = order.CouponCode,

        SubtotalAmount = order.SubtotalAmount,
        DiscountAmount = order.DiscountAmount,
        TotalAmount = order.TotalAmount,

        PaymentMethod = order.PaymentMethod,
        Status = order.Status,
        PaymentStatus = order.PaymentStatus,

        CreatedAtUtc = order.CreatedAtUtc,
        ConfirmedAtUtc = order.ConfirmedAtUtc,

        Details = order.OrderDetails.Select(detail =>
            new OrderDetailDTO
            {
                ProductId = detail.ProductId,
                ProductName = detail.ProductName,
                ProductDescription = detail.ProductDescription,
                UnitPrice = detail.UnitPrice,
                Quantity = detail.Quantity,
                LineTotal = detail.LineTotal
            }).ToList()
    };
}
