
    namespace VShop.CartiApi.Models
    {
        public class Cart
        {
            public CartHeader CartHeader { get; set; } = new CartHeader();

            public IEnumerable<CartItem> CartItems { get; set; } = Enumerable.Empty<CartItem>();
        }
    }
