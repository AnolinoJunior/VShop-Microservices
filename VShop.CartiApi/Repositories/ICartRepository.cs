
using VShop.CartiApi.DTOs;

namespace VShop.CartiApi.Repositories
{
    public interface ICartRepository
    {
        // =====================================
        // 1 - BUSCAR CARRINHO PELO USUÁRIO
        // =====================================

        Task<CartDTO> GetCartByUserIdAsync(string userId);

        // =====================================
        // 2 - ADICIONAR / ATUALIZAR CARRINHO
        // =====================================

        Task<CartDTO> UpdateCartAsync(CartDTO cart);

        // =====================================
        // 3 - ATUALIZAR QUANTIDADE DO PRODUTO
        // =====================================

        Task<bool> UpdateItemQuantityAsync(
            string userId,
            int productId,
            int quantity);

        // =====================================
        // 4 - REMOVER PRODUTO DO CARRINHO
        // =====================================

        Task<bool> DeleteItemFromCartAsync(
            string userId,
            int productId);

        // =====================================
        // 5 - LIMPAR CARRINHO
        // =====================================

        Task<bool> CleanCartAsync(string userId);

        // =====================================
        // 6 - APLICAR CUPOM
        // =====================================

        Task<bool> ApplyCouponAsync(
            string userId,
            string couponCode);

        Task<decimal?> GetSubtotalAsync(string userId);
        Task<decimal?> GetVerifiedSubtotalAsync(string userId);
        Task<OrderSnapshotDTO?> GetOrderSnapshotAsync(string userId);

        // =====================================
        // 7 - REMOVER CUPOM
        // =====================================

        Task<bool> DeleteCouponAsync(string userId);

        Task<CheckoutHeaderDTO?> CheckoutAsync(CheckoutHeaderDTO checkout);
    }
}
