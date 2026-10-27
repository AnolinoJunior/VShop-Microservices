
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VShop.Web.Models;
using VShop.Web.Services.Contracts;

namespace VShop.Web.Services
{
    public class CartService : ICartService
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly JsonSerializerOptions _options;

        private const string apiEndpoint = "api/cart";

        public async Task<JsonElement> CheckoutOrderAsync(long orderId, bool simulateDecline, string token)
        {
            if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiEndpoint}/orders/{orderId}/checkout")
            {
                Content = JsonContent.Create(new { simulateDecline })
            };
            PutTokenInHeaderAuthorization(token, request);
            using var response = await _clientFactory.CreateClient("CartApi").SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Erro no checkout: {(int)response.StatusCode}.", null, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        public async Task<JsonElement> PrepareOrderAsync(PrepareOrderRequest input, string token)
        {
            ArgumentNullException.ThrowIfNull(input);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiEndpoint}/orders")
            {
                Content = JsonContent.Create(input)
            };
            PutTokenInHeaderAuthorization(token, request);
            using var response = await _clientFactory.CreateClient("CartApi").SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Erro ao preparar pedido: {(int)response.StatusCode}.", null, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        public CartService(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;

            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        // =====================================
        // 1 - BUSCAR CARRINHO PELO USUÁRIO
        // =====================================

        public async Task<CartViewModel?> GetCartByUserIdAsync(
            string userId,
            string token)
        {
            var client = _clientFactory.CreateClient("CartApi");

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{apiEndpoint}/getcart/{Uri.EscapeDataString(userId)}");

            PutTokenInHeaderAuthorization(token, request);

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var apiResponse =
                await response.Content.ReadAsStreamAsync();

            var cart = await JsonSerializer.DeserializeAsync<CartViewModel>(
                apiResponse,
                _options);

            if (cart?.CartHeader != null)
            {
                var subtotal = cart.CartItems?.Where(item => item.Product != null)
                    .Sum(item => Convert.ToDecimal(item.Product!.Price) * item.Quantity) ?? 0m;
                cart.CartHeader.SubtotalAmount = Convert.ToDouble(subtotal);
                if (!string.IsNullOrWhiteSpace(cart.CartHeader.CouponCode))
                {
                    try
                    {
                        var discountClient = _clientFactory.CreateClient("DiscountApi");
                        using var couponRequest = new HttpRequestMessage(
                            HttpMethod.Get,
                            $"api/coupon/validate/{Uri.EscapeDataString(cart.CartHeader.CouponCode)}");
                        PutTokenInHeaderAuthorization(token, couponRequest);
                        using var couponResponse = await discountClient.SendAsync(couponRequest);
                        if (couponResponse.IsSuccessStatusCode && subtotal >= 50m)
                        {
                            var coupon = await couponResponse.Content.ReadFromJsonAsync<CouponResponse>(_options);
                            cart.CartHeader.DiscountAmount = Convert.ToDouble(
                                Math.Min(subtotal, coupon?.DiscountAmount ?? 0m));
                        }
                    }
                    catch (HttpRequestException)
                    {
                        cart.CartHeader.DiscountAmount = 0d;
                    }
                }
                cart.CartHeader.TotalAmount = Math.Max(
                    0d,
                    cart.CartHeader.SubtotalAmount - cart.CartHeader.DiscountAmount);
            }
            return cart;
        }

        // =====================================
        // 2 - ADICIONAR PRODUTO AO CARRINHO
        // =====================================

        public async Task<CartViewModel?> AddItemToCartAsync(
            CartViewModel cartVM,
            string token)
        {
            ArgumentNullException.ThrowIfNull(cartVM);

            var client = _clientFactory.CreateClient("CartApi");

            var json = JsonSerializer.Serialize(cartVM, _options);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                apiEndpoint);

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            PutTokenInHeaderAuthorization(token, request);

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();

                throw new HttpRequestException(
                    $"Erro ao adicionar produto ao carrinho. " +
                    $"Status: {(int)response.StatusCode} " +
                    $"({response.StatusCode}). Detalhes: {error}");
            }

            if (response.Content.Headers.ContentLength == 0)
            {
                return cartVM;
            }

            await using var apiResponse =
                await response.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<CartViewModel>(
                apiResponse,
                _options);
        }

        // =====================================
        // 3 - ATUALIZAR CARRINHO
        // =====================================

        public async Task<CartViewModel?> UpdateCartAsync(
            CartViewModel cartVM,
            string token)
        {
            return await AddItemToCartAsync(cartVM, token);
        }

        // =====================================
        // 4 - ATUALIZAR QUANTIDADE DO PRODUTO
        // =====================================

        public async Task<bool> UpdateItemQuantityAsync(
            string userId,
            int productId,
            int quantity,
            string token)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "O ID do usuário é obrigatório.",
                    nameof(userId));
            }

            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(productId),
                    "O ID do produto deve ser maior que zero.");
            }

            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "A quantidade deve ser maior ou igual a 1.");
            }

            var client = _clientFactory.CreateClient("CartApi");

            // Endpoint da CartApi:
            // PATCH api/cart/{userId}/{productId}/quantity

            var endpoint =
                $"{apiEndpoint}/{Uri.EscapeDataString(userId)}/{productId}/quantity";

            // A CartApi espera um número inteiro em JSON.
            // Exemplo de body: 2
            var json = JsonSerializer.Serialize(quantity);

            using var request = new HttpRequestMessage(
                HttpMethod.Patch,
                endpoint);

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            PutTokenInHeaderAuthorization(token, request);

            using var response = await client.SendAsync(request);

            // 204 No Content: quantidade atualizada.
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // 404 Not Found: produto não encontrado.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            // Outros erros: preservar detalhes.
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Erro ao atualizar a quantidade do produto. " +
                $"Status: {(int)response.StatusCode} " +
                $"({response.StatusCode}). Detalhes: {error}");
        }

        // =====================================
        // 5 - REMOVER PRODUTO DO CARRINHO
        // =====================================

        public async Task<bool> RemoveItemFromCartAsync(
            string userId,
            int productId,
            string token)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "O ID do usuário é obrigatório.",
                    nameof(userId));
            }

            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(productId),
                    "O ID do produto deve ser maior que zero.");
            }

            var client = _clientFactory.CreateClient("CartApi");

            // Endpoint da CartApi:
            // DELETE api/cart/{userId}/{productId}

            var endpoint =
                $"{apiEndpoint}/{Uri.EscapeDataString(userId)}/{productId}";

            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                endpoint);

            PutTokenInHeaderAuthorization(token, request);

            using var response = await client.SendAsync(request);

            // 204 No Content: produto removido.
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // 404 Not Found: produto ou carrinho inexistente.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Erro ao remover produto do carrinho. " +
                $"Status: {(int)response.StatusCode} " +
                $"({response.StatusCode}). Detalhes: {error}");
        }

        // =====================================
        // 6 - APLICAR CUPOM
        // =====================================

        public async Task<bool> ApplyCouponAsync(
            CartViewModel cartVM,
            string couponCode,
            string token)
        {
            if (cartVM?.CartHeader == null || string.IsNullOrWhiteSpace(couponCode)) return false;
            var client = _clientFactory.CreateClient("CartApi");
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{apiEndpoint}/coupon/{Uri.EscapeDataString(cartVM.CartHeader.UserId)}");
            request.Content = new StringContent(JsonSerializer.Serialize(couponCode.Trim()), Encoding.UTF8, "application/json");
            PutTokenInHeaderAuthorization(token, request);
            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode) return true;
            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var validationError = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(validationError);
            }
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Erro ao aplicar cupom. Status: {(int)response.StatusCode}. Detalhes: {error}");
        }

        // =====================================
        // 7 - REMOVER CUPOM
        // =====================================

        public async Task<bool> DeleteCouponAsync(
            string userId,
            string token)
        {
            var client = _clientFactory.CreateClient("CartApi");
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{apiEndpoint}/coupon/{Uri.EscapeDataString(userId)}");
            PutTokenInHeaderAuthorization(token, request);
            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode) return true;
            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            throw new HttpRequestException($"Erro ao remover cupom. Status: {(int)response.StatusCode}.");
        }

        // =====================================
        // 8 - LIMPAR CARRINHO
        // =====================================

        public Task<bool> ClearCartAsync(
            string userId,
            string token)
        {
            throw new NotImplementedException();
        }

        // =====================================
        // 9 - CHECKOUT (FUTURAMENTE)
        // =====================================

        public async Task<CartViewModel?> CheckoutAsync(
            CartHeaderViewModel cartHeader,
            string token)
        {
            ArgumentNullException.ThrowIfNull(cartHeader);
            var client = _clientFactory.CreateClient("CartApi");
            var payload = new { cartHeader.UserId, cartHeader.FirstName, cartHeader.LastName, cartHeader.Email, cartHeader.Telephone };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiEndpoint}/checkout")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, _options), Encoding.UTF8, "application/json")
            };
            PutTokenInHeaderAuthorization(token, request);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode is 400 or 422) throw new InvalidOperationException(error);
                throw new HttpRequestException($"Erro no checkout. Status: {(int)response.StatusCode}. Detalhes: {error}");
            }
            var result = await response.Content.ReadFromJsonAsync<CartHeaderViewModel>(_options);
            return result == null ? null : new CartViewModel { CartHeader = result, CartItems = Array.Empty<CartItemViewModel>() };
        }

        // =====================================
        // MÉTODO AUXILIAR - TOKEN JWT
        // =====================================

        private static void PutTokenInHeaderAuthorization(
            string token,
            HttpRequestMessage request)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException(
                    "O token JWT é obrigatório.",
                    nameof(token));
            }

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);
        }
    }

    internal sealed class CouponResponse
    {
        public decimal DiscountAmount { get; set; }
        public bool IsActive { get; set; }
    }
}
