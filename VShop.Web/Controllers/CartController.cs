
using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VShop.Web.Models;
using VShop.Web.Services.Contracts;

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
        // 2 - ATUALIZAR QUANTIDADE DO PRODUTO
        // =====================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateItemQuantity(
            int productId,
            int quantity)
        {
            // Valida os dados recebidos.
            if (productId <= 0 || quantity < 1)
            {
                TempData["ErrorMessage"] =
                    "Produto ou quantidade inválida.";

                return RedirectToAction(nameof(Index));
            }

            // Obtém o ID do usuário autenticado.
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // Obtém o token JWT.
            var token = await GetAccessToken();

            if (string.IsNullOrWhiteSpace(token))
            {
                return Challenge();
            }

            // Atualiza a quantidade na CartApi.
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

            // Recarrega o carrinho com os novos valores.
            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // 3 - REMOVER PRODUTO DO CARRINHO
        // =====================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int id)
        {
            if (id <= 0)
            {
                return BadRequest("ID do produto inválido.");
            }

            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var token = await GetAccessToken();

            if (string.IsNullOrWhiteSpace(token))
            {
                return Challenge();
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyCoupon(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode) || couponCode.Trim().Length > 50)
            {
                TempData["ErrorMessage"] = "Informe um código de cupom válido.";
                return RedirectToAction(nameof(Index));
            }

            var userId = GetUserId();
            var token = await GetAccessToken();
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token)) return Challenge();

            try
            {
                var cart = await _cartService.GetCartByUserIdAsync(userId, token);
                if (cart == null || !await _cartService.ApplyCouponAsync(cart, couponCode, token))
                {
                    TempData["ErrorMessage"] = "Cupom inexistente, inativo ou inválido.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Cupom aplicado com sucesso!";
                }
            }
            catch (HttpRequestException)
            {
                TempData["ErrorMessage"] = "Não foi possível validar o cupom no momento.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCoupon()
        {
            var userId = GetUserId();
            var token = await GetAccessToken();
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token)) return Challenge();

            if (await _cartService.DeleteCouponAsync(userId, token))
                TempData["SuccessMessage"] = "Cupom removido com sucesso!";
            else
                TempData["ErrorMessage"] = "Não foi possível remover o cupom.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = await GetCartByUser();
            if (cart?.CartHeader == null || cart.CartItems == null || !cart.CartItems.Any())
            {
                TempData["ErrorMessage"] = "Adicione produtos ao carrinho antes de finalizar.";
                return RedirectToAction(nameof(Index));
            }
            cart.CartHeader.FirstName = User.FindFirst("given_name")?.Value ?? string.Empty;
            cart.CartHeader.LastName = User.FindFirst("family_name")?.Value ?? string.Empty;
            cart.CartHeader.Email = User.FindFirst("email")?.Value ?? string.Empty;
            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CartViewModel model)
        {
            var submittedHeader = model?.CartHeader ?? new CartHeaderViewModel();
            if (!ModelState.IsValid)
            {
                var current = await GetCartByUser();
                if (current != null)
                {
                    current.CartHeader.FirstName = submittedHeader.FirstName;
                    current.CartHeader.LastName = submittedHeader.LastName;
                    current.CartHeader.Email = submittedHeader.Email;
                    current.CartHeader.Telephone = submittedHeader.Telephone;
                }
                return View(model: current ?? new CartViewModel { CartHeader = submittedHeader });
            }

            var userId = GetUserId();
            var token = await GetAccessToken();
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token)) return Challenge();
            var cart = await _cartService.GetCartByUserIdAsync(userId, token);
            if (cart?.CartHeader == null || cart.CartItems == null || !cart.CartItems.Any())
            {
                TempData["ErrorMessage"] = "O carrinho está vazio.";
                return RedirectToAction(nameof(Index));
            }
            cart.CartHeader.FirstName = submittedHeader.FirstName;
            cart.CartHeader.LastName = submittedHeader.LastName;
            cart.CartHeader.Email = submittedHeader.Email;
            cart.CartHeader.Telephone = submittedHeader.Telephone;
            try
            {
                var result = await _cartService.CheckoutAsync(cart.CartHeader, token);
                return result?.CartHeader == null ? View("Checkout", cart) : View("CheckoutCompleted", result);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Checkout", cart);
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "Os serviços estão indisponíveis. Tente novamente.");
                return View("Checkout", cart);
            }
        }

        // =====================================
        // 4 - BUSCAR CARRINHO DO USUÁRIO
        // =====================================

        private async Task<CartViewModel?> GetCartByUser()
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var token = await GetAccessToken();

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "Não foi possível obter o token de acesso.");
            }

            var cart = await _cartService
                .GetCartByUserIdAsync(userId, token);

            if (cart?.CartHeader == null)
            {
                return null;
            }

            // =====================================
            // RECALCULAR TOTAL DO CARRINHO
            // =====================================

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

            cart.CartHeader.SubtotalAmount = Convert.ToDouble(totalAmount);
            var discount = Convert.ToDecimal(cart.CartHeader.DiscountAmount);
            cart.CartHeader.DiscountAmount = Convert.ToDouble(Math.Min(discount, totalAmount));
            cart.CartHeader.TotalAmount =
                Convert.ToDouble(Math.Max(0m, totalAmount - discount));

            if (!string.IsNullOrWhiteSpace(cart.CartHeader.CouponCode) && totalAmount < 50m)
            {
                TempData["ErrorMessage"] =
                    "O subtotal mínimo de R$ 50,00 não foi atingido. O desconto não foi aplicado.";
            }

            return cart;
        }

        // =====================================
        // 5 - OBTER TOKEN DE ACESSO
        // =====================================

        private async Task<string?> GetAccessToken()
        {
            return await HttpContext.GetTokenAsync(
                "access_token");
        }

        // =====================================
        // 6 - OBTER ID DO USUÁRIO
        // =====================================

        private string? GetUserId()
        {
            return User.FindFirst("sub")?.Value
                ?? User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
