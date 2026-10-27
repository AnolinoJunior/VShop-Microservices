
namespace VShop.CartiApi.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        public int Quantity { get; set; } = 1;

        public int ProductId { get; set; }

        public int CartHeaderId { get; set; }

        // Relacionamento com o produto
        public Product Product { get; set; } = null!;

        // Relacionamento com o cabeçalho do carrinho
        public CartHeader CartHeader { get; set; } = null!;
    }
}
