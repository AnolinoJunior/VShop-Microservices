using System.ComponentModel.DataAnnotations;

namespace Vshop.Web.Models
{
    public class ProductViewModel
    {
        public int ProductId { get; set; }

        [Required(ErrorMessage = "The Name is Required")]
        [MinLength(
            3,
            ErrorMessage = "The Name must have at least 3 characters")]
        [MaxLength(
            50,
            ErrorMessage = "The Name must have a maximum of 50 characters")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "The Price is Required")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "The Description is Required")]
        [MinLength(
            10,
            ErrorMessage = "The Description must have at least 10 characters")]
        [MaxLength(
            500,
            ErrorMessage = "The Description must have a maximum of 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "The Stock is Required")]
        [Range(
            1,
            99999,
            ErrorMessage = "The Stock must be between 1 and 99999")]
        public long Stock { get; set; }

        [Range (1,100)]
        public int Quantity { get; set; } = 1;

        public string? ImageUrl { get; set; }

        public string? CategoryName { get; set; }

        [Display(Name = "Categorias")]
        public int CategoryId { get; set; }
    }
}