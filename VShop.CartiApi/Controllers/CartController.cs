
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VShop.CartiApi.DTOs;
using VShop.CartiApi.Repositories;
using VShop.CartiApi.Rules;

namespace VShop.CartiApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "ApiScope")]
    public class CartController : ControllerBase
    {
        private readonly ICartRepository _repository;
        private readonly IHttpClientFactory _clients;

        public CartController(
            ICartRepository repository,
            IHttpClientFactory clients)
        {
            _repository = repository;
            _clients = clients;
        }

        // =====================================
        // MÉTODOS AUXILIARES - AUTENTICAÇÃO
        // =====================================

        private string? GetAuthenticatedUserId()
        {
            return User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private bool IsCurrentUser(string? userId)
        {
            var authenticatedUserId = GetAuthenticatedUserId();

            return !string.IsNullOrWhiteSpace(authenticatedUserId)
                && !string.IsNullOrWhiteSpace(userId)
                && authenticatedUserId == userId;
        }

        // =====================================
        // 1 - BUSCAR CARRINHO PELO USUÁRIO
        // GET: api/cart/getcart/{id}
        // =====================================

        [HttpGet("getcart/{id}")]
        public async Task<ActionResult<CartDTO>> GetByUserId(
            string id)
        {
            if (!IsCurrentUser(id))
                return Forbid();

            var cartDto = await _repository
                .GetCartByUserIdAsync(id);

            if (cartDto == null ||
                cartDto.CartHeader == null ||
                cartDto.CartHeader.Id == 0)
            {
                return NotFound();
            }

            return Ok(cartDto);
        }

        // =====================================
        // 2 - ADICIONAR OU ATUALIZAR CARRINHO
        // POST: api/cart
        // =====================================

        [HttpPost]
        public async Task<ActionResult<CartDTO>> UpdateCart(
            [FromBody] CartDTO cartDto)
        {
            if (cartDto == null ||
                cartDto.CartHeader == null)
            {
                return BadRequest(
                    "Os dados do carrinho são obrigatórios.");
            }

            var authenticatedUserId = GetAuthenticatedUserId();

            if (string.IsNullOrWhiteSpace(authenticatedUserId))
                return Forbid();

            if (!IsCurrentUser(cartDto.CartHeader.UserId))
                return Forbid();

            if (cartDto.CartItems == null ||
                !cartDto.CartItems.Any())
            {
                return BadRequest(
                    "O carrinho deve possuir pelo menos um item.");
            }

            if (cartDto.CartItems.Any(item =>
                item.ProductId <= 0 ||
                item.Quantity <= 0))
            {
                return BadRequest(
                    "Os itens do carrinho possuem dados inválidos.");
            }

            var updatedCart = await _repository
                .UpdateCartAsync(cartDto);

            return Ok(updatedCart);
        }

        // =====================================
        // 3 - ATUALIZAR QUANTIDADE
        // PATCH: api/cart/{userId}/{productId}/quantity
        // =====================================

        [HttpPatch("{userId}/{productId:int}/quantity")]
        public async Task<IActionResult> UpdateItemQuantity(
            string userId,
            int productId,
            [FromBody] int quantity)
        {
            if (!IsCurrentUser(userId))
                return Forbid();

            if (productId <= 0)
            {
                return BadRequest(
                    "O identificador do produto é inválido.");
            }

            if (quantity < 1)
            {
                return BadRequest(
                    "A quantidade deve ser maior ou igual a 1.");
            }

            var result = await _repository
                .UpdateItemQuantityAsync(
                    userId,
                    productId,
                    quantity);

            if (!result)
            {
                return NotFound(
                    "Produto não encontrado no carrinho.");
            }

            return NoContent();
        }

        // =====================================
        // 4 - REMOVER PRODUTO DO CARRINHO
        // DELETE: api/cart/{userId}/{productId}
        // =====================================

        [HttpDelete("{userId}/{productId:int}")]
        public async Task<IActionResult> RemoveFromCart(
            string userId,
            int productId)
        {
            if (!IsCurrentUser(userId))
                return Forbid();

            if (productId <= 0)
            {
                return BadRequest(
                    "O identificador do produto é inválido.");
            }

            var result = await _repository
                .DeleteItemFromCartAsync(userId, productId);

            if (!result)
                return NotFound();

            return NoContent();
        }

        // =====================================
        // 5 - LIMPAR CARRINHO
        // DELETE: api/cart/clear/{userId}
        // =====================================

        [HttpDelete("clear/{userId}")]
        public async Task<IActionResult> CleanCart(
            string userId)
        {
            if (!IsCurrentUser(userId))
                return Forbid();

            var result = await _repository
                .CleanCartAsync(userId);

            if (!result)
                return NotFound();

            return NoContent();
        }

        // =====================================
        // 6 - CONSULTAR SUBTOTAL
        // GET: api/cart/subtotal
        // =====================================

        [HttpGet("subtotal")]
        public async Task<IActionResult> GetSubtotal()
        {
            var userId = GetAuthenticatedUserId();

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var subtotal = await _repository
                .GetSubtotalAsync(userId);

            return subtotal is null
                ? NotFound()
                : Ok(subtotal.Value);
        }

        // =====================================
        // 7 - CONSULTAR SUBTOTAL VERIFICADO
        // GET: api/cart/verified-subtotal
        // =====================================

        [HttpGet("verified-subtotal")]
        public async Task<IActionResult> GetVerifiedSubtotal()
        {
            var userId = GetAuthenticatedUserId();

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            try
            {
                var subtotal = await _repository
                    .GetVerifiedSubtotalAsync(userId);

                return subtotal is null
                    ? NotFound()
                    : Ok(subtotal.Value);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ex.Message);
            }
        }

        // =====================================
        // 8 - SNAPSHOT DO CARRINHO
        // GET: api/cart/order-snapshot
        // =====================================

        [HttpGet("order-snapshot")]
        public async Task<IActionResult> GetOrderSnapshot()
        {
            var userId = GetAuthenticatedUserId();

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            try
            {
                var snapshot = await _repository
                    .GetOrderSnapshotAsync(userId);

                return snapshot is null
                    ? NotFound("Carrinho vazio ou inexistente.")
                    : Ok(snapshot);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ex.Message);
            }
        }

        // =====================================
        // 9 - PREPARAR PEDIDO
        // POST: api/cart/orders
        // =====================================

        [HttpPost("orders")]
        public async Task<IActionResult> PrepareOrder(
            [FromBody] PrepareOrderDTO input,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(GetAuthenticatedUserId()))
                return Forbid();

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/order/prepare")
            {
                Content = JsonContent.Create(input)
            };

            request.Headers.TryAddWithoutValidation(
                "Authorization",
                Request.Headers.Authorization.ToString());

            try
            {
                using var response = await _clients
                    .CreateClient("OrderApi")
                    .SendAsync(request, cancellationToken);

                var body = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = body,
                    ContentType = response.Content.Headers
                        .ContentType?.ToString()
                        ?? "application/json"
                };
            }
            catch (HttpRequestException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "A OrderApi está indisponível.");
            }
        }

        // =====================================
        // 10 - EXECUTAR CHECKOUT COM SAGA
        // POST: api/cart/orders/{id}/checkout
        // =====================================

        [HttpPost("orders/{id:long}/checkout")]
        public async Task<IActionResult> CheckoutOrder(
            long id,
            [FromBody] CheckoutSagaRequest input,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(GetAuthenticatedUserId()))
                return Forbid();

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/order/{id}/checkout")
            {
                Content = JsonContent.Create(input)
            };

            request.Headers.TryAddWithoutValidation(
                "Authorization",
                Request.Headers.Authorization.ToString());

            try
            {
                using var response = await _clients
                    .CreateClient("OrderApi")
                    .SendAsync(request, cancellationToken);

                var body = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = body,
                    ContentType = response.Content.Headers
                        .ContentType?.ToString()
                        ?? "application/json"
                };
            }
            catch (HttpRequestException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "A OrderApi está indisponível.");
            }
        }

        // =====================================
        // 11 - RECUPERAR PEDIDO POR IDEMPOTÊNCIA
        // GET: api/cart/orders/by-idempotency-key/{key}
        // =====================================

        [HttpGet("orders/by-idempotency-key/{key}")]
        public async Task<IActionResult> GetOrderByIdempotencyKey(
            string key,
            CancellationToken cancellationToken)
        {
            var userId = GetAuthenticatedUserId();

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            if (string.IsNullOrWhiteSpace(key) ||
                key.Length > 100)
            {
                return BadRequest(
                    "Chave de idempotência inválida.");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/order/by-idempotency-key/" +
                Uri.EscapeDataString(key));

            request.Headers.TryAddWithoutValidation(
                "Authorization",
                Request.Headers.Authorization.ToString());

            try
            {
                using var response = await _clients
                    .CreateClient("OrderApi")
                    .SendAsync(request, cancellationToken);

                var body = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = body,
                    ContentType = response.Content.Headers
                        .ContentType?.ToString()
                        ?? "application/json"
                };
            }
            catch (HttpRequestException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "A OrderApi está indisponível.");
            }
        }

        // =====================================
        // 12 - APLICAR CUPOM
        // POST: api/cart/coupon/{userId}
        // =====================================

        [HttpPost("coupon/{userId}")]
        public async Task<IActionResult> ApplyCoupon(
            string userId,
            [FromBody] string couponCode)
        {
            if (!IsCurrentUser(userId))
                return Forbid();

            if (string.IsNullOrWhiteSpace(couponCode))
            {
                return BadRequest(
                    "O código do cupom é obrigatório.");
            }

            var subtotal = await _repository
                .GetSubtotalAsync(userId);

            if (subtotal is null)
            {
                return NotFound(
                    "Carrinho não encontrado.");
            }

            if (subtotal < CouponRules.MinimumSubtotal)
            {
                return BadRequest(
                    $"O subtotal mínimo para utilizar cupons é de " +
                    $"R$ {CouponRules.MinimumSubtotal:N2}.");
            }

            bool result;

            try
            {
                result = await _repository
                    .ApplyCouponAsync(
                        userId,
                        couponCode.Trim());
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ex.Message);
            }

            if (!result)
                return NotFound();

            return Ok();
        }

        // =====================================
        // 13 - REMOVER CUPOM
        // DELETE: api/cart/coupon/{userId}
        // =====================================

        [HttpDelete("coupon/{userId}")]
        public async Task<IActionResult> DeleteCoupon(
            string userId)
        {
            if (!IsCurrentUser(userId))
                return Forbid();

            var result = await _repository
                .DeleteCouponAsync(userId);

            if (!result)
                return NotFound();

            return NoContent();
        }

        // =====================================
        // 14 - CHECKOUT LEGADO
        // POST: api/cart/checkout
        // =====================================

        [HttpPost("checkout")]
        public async Task<ActionResult<CheckoutHeaderDTO>> Checkout(
            [FromBody] CheckoutHeaderDTO checkout)
        {
            var authenticatedUserId =
                GetAuthenticatedUserId();

            if (string.IsNullOrWhiteSpace(authenticatedUserId) ||
                checkout == null)
            {
                return BadRequest(
                    "Os dados do checkout são obrigatórios.");
            }

            if (!IsCurrentUser(checkout.UserId))
                return Forbid();

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                var result = await _repository
                    .CheckoutAsync(checkout);

                return result is null
                    ? BadRequest(
                        "O carrinho não possui produtos válidos.")
                    : Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ex.Message);
            }
        }
    }
}
