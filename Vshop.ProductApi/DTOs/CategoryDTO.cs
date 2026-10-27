using System.ComponentModel.DataAnnotations;

namespace Vshop.ProductApi.DTOs
{
    public class CategoryDTO
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "The Name is Required")]
        [MinLength(3, ErrorMessage = "The Name must have at least 3 characters")]
        [MaxLength(50, ErrorMessage = "The Name must have a maximum of 50 characters")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<ProductDTO> Products { get; set; } = new List<ProductDTO>();
    }
}