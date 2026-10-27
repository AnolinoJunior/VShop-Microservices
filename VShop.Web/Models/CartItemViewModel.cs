using Vshop.Web.Models;

namespace VShop.Web.Models
{
    public class CartItemViewModel
    {
        public int Id { get; set; }
        public ProductViewModel? Product { get; set; }
        public int Quantity { get; set; }

        public int ProductId { get; set; }

        public int CartHeaderId { get; set; }
    }
}
