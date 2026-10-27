
using VShop.Web.Models;

namespace VShop.Web.Services.Contracts
{
    public interface ICartService
    {
        Task<System.Text.Json.JsonElement> PrepareOrderAsync(PrepareOrderRequest request, string token);
        Task<System.Text.Json.JsonElement> CheckoutOrderAsync(long orderId, bool simulateDecline, string token);
        // =====================================
        // 1 - BUSCAR CARRINHO PELO USUÁRIO
        // =====================================

        Task<CartViewModel?> GetCartByUserIdAsync(
            string userId,
            string token);

        // =====================================
        // 2 - ADICIONAR PRODUTO AO CARRINHO
        // =====================================

        Task<CartViewModel?> AddItemToCartAsync(
            CartViewModel cartVM,
            string token);

        // =====================================
        // 3 - ATUALIZAR CARRINHO
        // =====================================

        Task<CartViewModel?> UpdateCartAsync(
            CartViewModel cartVM,
            string token);

        // =====================================
        // 4 - ATUALIZAR QUANTIDADE DO PRODUTO
        // =====================================

        Task<bool> UpdateItemQuantityAsync(
            string userId,
            int productId,
            int quantity,
            string token);

        // =====================================
        // 5 - REMOVER PRODUTO DO CARRINHO
        // =====================================

        Task<bool> RemoveItemFromCartAsync(
            string userId,
            int productId,
            string token);

        // =====================================
        // 6 - APLICAR CUPOM DE DESCONTO
        // =====================================

        Task<bool> ApplyCouponAsync(
            CartViewModel cartVM,
            string couponCode,
            string token);

        // =====================================
        // 7 - REMOVER CUPOM
        // =====================================

        Task<bool> DeleteCouponAsync(
            string userId,
            string token);

        // =====================================
        // 8 - LIMPAR CARRINHO
        // =====================================

        Task<bool> ClearCartAsync(
            string userId,
            string token);

        // =====================================
        // 9 - CHECKOUT (FUTURAMENTE)
        // =====================================

        Task<CartViewModel?> CheckoutAsync(
            CartHeaderViewModel cartHeader,
            string token);
    }
}
