
    using System;
    using System.Linq;
    using System.Text.Json;
    using System.Security.Claims;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using VShop.Web.Models;
    using VShop.Web.Services.Contracts;
    using Duende.AccessTokenManagement.OpenIdConnect;

namespace VShop.Web.Controllers
    {
        [Authorize]
        public class CartController : Controller
        {
            private readonly ICartService _cartService;

            public CartController(ICartService cartService)
            {
                _cartService = cartService;
            }

            // =====================================
            // 1 - EXIBIR CARRINHO
            // =====================================

            [HttpGet]
            public async Task<IActionResult> Index()
            {
                var cartVM = await GetCartByUser();

                if (cartVM == null ||
                    cartVM.CartHeader == null ||
                    cartVM.CartItems == null ||
                    !cartVM.CartItems.Any())
                {
                    ModelState.AddModelError(
                        "CartNotFound",
                        "Seu carrinho está vazio. Continue comprando!");

                    return View("CartNotFound");
                }

                return View(cartVM);
            }

            // =====================================
            // 2 - ATUALIZAR QUANTIDADE
            // =====================================

            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> UpdateItemQuantity(
                int productId,
                int quantity)
            {
                if (productId <= 0 || quantity < 1)
                {
                    TempData["ErrorMessage"] =
                        "Produto ou quantidade inválida.";

                    return RedirectToAction(nameof(Index));
                }

                var userId = GetUserId();

                if (string.IsNullOrWhiteSpace(userId))
                    return Challenge();

                var token = await GetAccessToken();

                if (string.IsNullOrWhiteSpace(token))
                    return Challenge();

                var result = await _cartService
                    .UpdateItemQuantityAsync(
                        userId,
                        productId,
                        quantity,
                        token);

                if (result)
                {
                    TempData["SuccessMessage"] =
                        "Quantidade atualizada com sucesso!";
                }
                else
                {
                    TempData["ErrorMessage"] =
                        "Não foi possível atualizar a quantidade do produto.";
                }

                return RedirectToAction(nameof(Index));
            }

            // =====================================
            // 3 - REMOVER PRODUTO
            // =====================================

            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> RemoveItem(int id)
            {
                if (id <= 0)
                    return BadRequest("ID do produto inválido.");

                var userId = GetUserId();

                if (string.IsNullOrWhiteSpace(userId))
                    return Challenge();

                var token = await GetAccessToken();

                if (string.IsNullOrWhiteSpace(token))
                    return Challenge();

                var result = await _cartService
                    .RemoveItemFromCartAsync(
                        userId,
                        id,
                        token);

                if (result)
                {
                    TempData["SuccessMessage"] =
                        "Produto removido do carrinho com sucesso!";

                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError(
                    "RemoveItemError",
                    "Não foi possível remover o produto do carrinho.");

                return View("RemoveItem", id);
            }

            // =====================================
            // 4 - APLICAR CUPOM
            // =====================================

            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> ApplyCoupon(
                string couponCode)
            {
                if (string.IsNullOrWhiteSpace(couponCode) ||
                    couponCode.Trim().Length > 50)
                {
                    TempData["ErrorMessage"] =
                        "Informe um código de cupom válido.";

                    return RedirectToAction(nameof(Index));
                }

                var userId = GetUserId();
                var token = await GetAccessToken();

                if (string.IsNullOrWhiteSpace(userId) ||
                    string.IsNullOrWhiteSpace(token))
                    return Challenge();

                try
                {
                    var cart = await _cartService
                        .GetCartByUserIdAsync(userId, token);

                    if (cart == null ||
                        !await _cartService.ApplyCouponAsync(
                            cart, couponCode, token))
                    {
                        TempData["ErrorMessage"] =
                            "Cupom inexistente, inativo ou inválido.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] =
                            "Cupom aplicado com sucesso!";
                    }
                }
                catch (HttpRequestException)
                {
                    TempData["ErrorMessage"] =
                        "Não foi possível validar o cupom no momento.";
                }
                catch (InvalidOperationException ex)
                {
                    TempData["ErrorMessage"] = ex.Message;
                }

                return RedirectToAction(nameof(Index));
            }

            // =====================================
            // 5 - REMOVER CUPOM
            // =====================================

            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> DeleteCoupon()
            {
                var userId = GetUserId();
                var token = await GetAccessToken();

                if (string.IsNullOrWhiteSpace(userId) ||
                    string.IsNullOrWhiteSpace(token))
                    return Challenge();

                if (await _cartService.DeleteCouponAsync(
                    userId, token))
                {
                    TempData["SuccessMessage"] =
                        "Cupom removido com sucesso!";
                }
                else
                {
                    TempData["ErrorMessage"] =
                        "Não foi possível remover o cupom.";
                }

                return RedirectToAction(nameof(Index));
            }

            // =====================================
            // 6 - CHECKOUT GET
            // =====================================

            [HttpGet]
            public async Task<IActionResult> Checkout()
            {
                var cart = await GetCartByUser();

                if (cart?.CartHeader == null ||
                    cart.CartItems == null ||
                    !cart.CartItems.Any())
                {
                    TempData["ErrorMessage"] =
                        "Adicione produtos ao carrinho antes de finalizar.";

                    return RedirectToAction(nameof(Index));
                }

                cart.CartHeader.FirstName =
                    User.FindFirst("given_name")?.Value
                    ?? string.Empty;

                cart.CartHeader.LastName =
                    User.FindFirst("family_name")?.Value
                    ?? string.Empty;

                cart.CartHeader.Email =
                    User.FindFirst("email")?.Value
                    ?? string.Empty;

                // Mantém a chave para a mesma tentativa de compra.
                if (TempData.Peek("CheckoutIdempotencyKey") is not string key ||
                    string.IsNullOrWhiteSpace(key))
                {
                    TempData["CheckoutIdempotencyKey"] =
                        Guid.NewGuid().ToString("N");
                }

                return View(cart);
            }

            // =====================================
            // 7 - CHECKOUT POST COM RECUPERAÇÃO
            // =====================================

            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Checkout(CartViewModel model)
            {
                var submittedHeader = model?.CartHeader ?? new CartHeaderViewModel();

                if (!ModelState.IsValid)
                {
                    var current = await GetCartByUser();
                    if (current?.CartHeader != null)
                        CopyCustomerData(current.CartHeader, submittedHeader);

                    return View("Checkout", current ?? new CartViewModel
                    {
                        CartHeader = submittedHeader
                    });
                }

                var userId = GetUserId();
                var token = await GetAccessToken();

                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
                    return Challenge();

                var cart = await _cartService.GetCartByUserIdAsync(userId, token);
                if (cart?.CartHeader == null || cart.CartItems == null || !cart.CartItems.Any())
                {
                    TempData["ErrorMessage"] = "O carrinho está vazio.";
                    return RedirectToAction(nameof(Index));
                }

                CopyCustomerData(cart.CartHeader, submittedHeader);

                var key = TempData.Peek("CheckoutIdempotencyKey") as string;
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = Guid.NewGuid().ToString("N");
                    TempData["CheckoutIdempotencyKey"] = key;
                }

                try
                {
                    // Antes de preparar outro pedido, consulte a chave atual.
                    var existing = await _cartService.GetOrderByIdempotencyKeyAsync(key, token);
                    if (existing.HasValue)
                        return await HandleRecoveredOrderAsync(existing.Value, cart, userId, token);

                    var request = new PrepareOrderRequest
                    {
                        FirstName = submittedHeader.FirstName,
                        LastName = submittedHeader.LastName,
                        Email = submittedHeader.Email,
                        Telephone = submittedHeader.Telephone,
                        IdempotencyKey = key
                    };

                    var prepared = await _cartService.PrepareOrderAsync(request, token);
                    if (!TryGetInt64(prepared, "orderHeaderId", out var orderId) || orderId <= 0)
                        throw new InvalidOperationException("A OrderApi não retornou um identificador de pedido válido.");

                    // false = pagamento simulado aprovado; nunca cobrar pagamento real aqui.
                    var checkout = await _cartService.CheckoutOrderAsync(orderId, false, token);
                    var phase = GetString(checkout, "phase");
                    var status = GetString(checkout, "orderStatus");

                    if (EqualsIgnoreCase(phase, "Completed") && EqualsIgnoreCase(status, "Confirmed"))
                        return await CompleteCheckoutAsync(orderId, cart, userId, token);

                    if (EqualsIgnoreCase(phase, "Failed"))
                    {
                        // A saga terminou com falha. Uma nova tentativa precisa de outra chave.
                        TempData["CheckoutIdempotencyKey"] = Guid.NewGuid().ToString("N");
                        return CheckoutError(cart,
                            $"O pedido #{orderId} falhou. O carrinho foi mantido. " +
                            "Confirme a compensação antes de tentar novamente.");
                    }

                    return CheckoutError(cart,
                        $"O pedido #{orderId} ainda está em processamento. " +
                        "Não faça outra tentativa até confirmar o resultado.");
                }
                catch (HttpRequestException)
                {
                    // Uma falha de rede NÃO prova que o pedido não foi criado.
                    // Tenta consultar a mesma chave sem gerar outra.
                    try
                    {
                        var recovered = await _cartService.GetOrderByIdempotencyKeyAsync(key, token);
                        if (recovered.HasValue)
                            return await HandleRecoveredOrderAsync(recovered.Value, cart, userId, token);
                    }
                    catch (HttpRequestException)
                    {
                        // Estado desconhecido: preservar a chave e o carrinho.
                    }
                    catch (JsonException)
                    {
                        // Resposta de recuperação inválida: não iniciar outra compra.
                    }

                    return CheckoutError(cart,
                        "Não foi possível confirmar o resultado da compra. " +
                        "A chave da tentativa foi preservada. Não tente novamente até consultar o pedido.");
                }
                catch (InvalidOperationException ex)
                {
                    return CheckoutError(cart, ex.Message);
                }
                catch (JsonException)
                {
                    return CheckoutError(cart,
                        "A API retornou uma resposta inesperada. " +
                        "Não inicie outra compra até verificar o pedido.");
                }
            }

            private async Task<IActionResult> HandleRecoveredOrderAsync(
                JsonElement order,
                CartViewModel cart,
                string userId,
                string token)
            {
                if (!TryGetInt64(order, "orderHeaderId", out var orderId) || orderId <= 0)
                    return CheckoutError(cart, "Pedido recuperado sem identificador válido.");

                var status = GetString(order, "status");

                if (status is null &&
                    order.TryGetProperty("status", out var statusElement) &&
                    statusElement.ValueKind == JsonValueKind.Number &&
                    statusElement.TryGetInt32(out var statusValue))
                {
                    status = statusValue switch
                    {
                        0 => "Pending",
                        1 => "StockReserved",
                        2 => "CouponReserved",
                        3 => "PaymentApproved",
                        4 => "Confirmed",
                        5 => "Failed",
                        6 => "Cancelled",
                        _ => null
                    };
                }
                if (EqualsIgnoreCase(status, "Confirmed"))
                    return await CompleteCheckoutAsync(orderId, cart, userId, token);

                if (EqualsIgnoreCase(status, "Failed") ||
                    EqualsIgnoreCase(status, "Cancelled") ||
                    EqualsIgnoreCase(status, "Canceled"))
                {
                    // O pedido está encerrado.
                    // A próxima tentativa precisa de uma nova chave de idempotência.
                    TempData["CheckoutIdempotencyKey"] = Guid.NewGuid().ToString("N");

                    return CheckoutError(cart,
                        $"O pedido #{orderId} está com status {status}. " +
                        "Uma nova tentativa de compra foi liberada. " +
                        "Confira os dados antes de finalizar novamente.");
                }

                // Um pedido Pending pode estar em processamento pela Saga.
                // Não iniciar uma segunda Saga automaticamente.
                return CheckoutError(cart,
                    $"O pedido #{orderId} já existe e está com status {status ?? "desconhecido"}. " +
                    "Aguarde ou consulte seus pedidos antes de tentar novamente.");
            }

            private async Task<IActionResult> CompleteCheckoutAsync(
                long orderId,
                CartViewModel cart,
                string userId,
                string token)
            {
                ViewBag.OrderId = orderId;
                ViewBag.CartCleared = false;

                try
                {
                    ViewBag.CartCleared = await _cartService.ClearCartAsync(userId, token);
                }
                catch (HttpRequestException)
                {
                    // Pedido confirmado, mas limpeza não garantida.
                }

                TempData.Remove("CheckoutIdempotencyKey");
                return View("CheckoutCompleted", cart);
            }

            private IActionResult CheckoutError(CartViewModel cart, string message)
            {
                ModelState.AddModelError(string.Empty, message);
                return View("Checkout", cart);
            }

            private static bool EqualsIgnoreCase(string? left, string right) =>
                string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

            private static void CopyCustomerData(
                CartHeaderViewModel destination,
                CartHeaderViewModel source)
            {
                destination.FirstName = source.FirstName;
                destination.LastName = source.LastName;
                destination.Email = source.Email;
                destination.Telephone = source.Telephone;
            }

            // =====================================
            // 8 - MÉTODOS AUXILIARES DO PEDIDO
            // =====================================

            private async Task<JsonDocument> PrepareOrderDocument(
                PrepareOrderRequest input,
                string token)
            {
                var response = await _cartService
                    .PrepareOrderAsync(input, token);

                return JsonDocument.Parse(response.GetRawText());
            }

            private static string? GetString(
                JsonElement obj,
                string name)
            {
                if (obj.ValueKind != JsonValueKind.Object)
                    return null;

                foreach (var prop in obj.EnumerateObject())
                {
                    if (string.Equals(
                        prop.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return prop.Value.ValueKind ==
                            JsonValueKind.String
                            ? prop.Value.GetString()
                            : null;
                    }
                }

                return null;
            }

            private static bool TryGetInt64(
                JsonElement obj,
                string name,
                out long result)
            {
                result = 0;

                if (obj.ValueKind != JsonValueKind.Object)
                    return false;

                foreach (var prop in obj.EnumerateObject())
                {
                    if (string.Equals(
                        prop.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return prop.Value.ValueKind ==
                            JsonValueKind.Number &&
                            prop.Value.TryGetInt64(out result);
                    }
                }

                return false;
            }

            // =====================================
            // 9 - BUSCAR CARRINHO
            // =====================================

            private async Task<CartViewModel?> GetCartByUser()
            {
                var userId = GetUserId();

                if (string.IsNullOrWhiteSpace(userId))
                    return null;

                var token = await GetAccessToken();

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException(
                        "Não foi possível obter o token de acesso.");
                }

                var cart = await _cartService
                    .GetCartByUserIdAsync(userId, token);

                if (cart?.CartHeader == null)
                    return null;

                decimal totalAmount = 0m;

                if (cart.CartItems != null)
                {
                    foreach (var item in cart.CartItems)
                    {
                        if (item.Product != null)
                        {
                            decimal price =
                                Convert.ToDecimal(item.Product.Price);

                            decimal quantity =
                                Convert.ToDecimal(item.Quantity);

                            totalAmount += price * quantity;
                        }
                    }
                }

                cart.CartHeader.SubtotalAmount =
                    Convert.ToDouble(totalAmount);

                var discount =
                    Convert.ToDecimal(cart.CartHeader.DiscountAmount);

                cart.CartHeader.DiscountAmount =
                    Convert.ToDouble(
                        Math.Min(discount, totalAmount));

                cart.CartHeader.TotalAmount =
                    Convert.ToDouble(
                        Math.Max(0m, totalAmount - discount));

                if (!string.IsNullOrWhiteSpace(
                        cart.CartHeader.CouponCode) &&
                    totalAmount < 50m)
                {
                    TempData["ErrorMessage"] =
                        "O subtotal mínimo de R$ 50,00 não foi atingido. " +
                        "O desconto não foi aplicado.";
                }

                return cart;
            }

        // =====================================
        // 10 - TOKEN JWT
        // =====================================

        private async Task<string?> GetAccessToken()
        {
            var result = await HttpContext.GetUserAccessTokenAsync();

            return result.Succeeded
                ? result.Token.AccessToken
                : null;
        }

        // =====================================
        // 11 - USUÁRIO AUTENTICADO
        // =====================================

        private string? GetUserId()
            {
                return User.FindFirst("sub")?.Value
                    ?? User.FindFirst(
                        ClaimTypes.NameIdentifier)?.Value;
            }
        }
    }
