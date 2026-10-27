namespace VShop.CartiApi.DTOs
{
    public class ProductDTO
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string? Description { get; set; } = string.Empty;

        public long Stock { get; set; }

        public string? ImageUrl { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;
    }
}
