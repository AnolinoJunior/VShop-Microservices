
using System.Text.Json;
using VShop.Web.Models;

namespace VShop.Web.Services.Contracts
{
    public interface ICartService
    {
        // =====================================
        // 1 - PREPARAR PEDIDO
        // =====================================

        Task<JsonElement> PrepareOrderAsync(
            PrepareOrderRequest request,
            string token);

        // =====================================
        // 2 - EXECUTAR CHECKOUT COM SAGA
        // =====================================

        Task<JsonElement> CheckoutOrderAsync(
            long orderId,
            bool simulateDecline,
            string token);

        // =====================================
        // 3 - RECUPERAR PEDIDO POR IDEMPOTÊNCIA
        // =====================================

        Task<JsonElement?> GetOrderByIdempotencyKeyAsync(
            string idempotencyKey,
            string token);

        // =====================================
        // 4 - BUSCAR CARRINHO PELO USUÁRIO
        // =====================================

        Task<CartViewModel?> GetCartByUserIdAsync(
            string userId,
            string token);

        // =====================================
        // 5 - ADICIONAR PRODUTO AO CARRINHO
        // =====================================

        Task<CartViewModel?> AddItemToCartAsync(
            CartViewModel cartVM,
            string token);

        // =====================================
        // 6 - ATUALIZAR CARRINHO
        // =====================================

        Task<CartViewModel?> UpdateCartAsync(
            CartViewModel cartVM,
            string token);

        // =====================================
        // 7 - ATUALIZAR QUANTIDADE DO PRODUTO
        // =====================================

        Task<bool> UpdateItemQuantityAsync(
            string userId,
            int productId,
            int quantity,
            string token);

        // =====================================
        // 8 - REMOVER PRODUTO DO CARRINHO
        // =====================================

        Task<bool> RemoveItemFromCartAsync(
            string userId,
            int productId,
            string token);

        // =====================================
        // 9 - APLICAR CUPOM DE DESCONTO
        // =====================================

        Task<bool> ApplyCouponAsync(
            CartViewModel cartVM,
            string couponCode,
            string token);

        // =====================================
        // 10 - REMOVER CUPOM
        // =====================================

        Task<bool> DeleteCouponAsync(
            string userId,
            string token);

        // =====================================
        // 11 - LIMPAR CARRINHO
        // =====================================

        Task<bool> ClearCartAsync(
            string userId,
            string token);

        // =====================================
        // 12 - CHECKOUT LEGADO
        // =====================================

        Task<CartViewModel?> CheckoutAsync(
            CartHeaderViewModel cartHeader,
            string token);
    }
}
