
using System.ComponentModel.DataAnnotations;

namespace VShop.Web.Models
{
    public class CartHeaderViewModel
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string CouponCode { get; set; } = string.Empty;

        public double TotalAmount { get; set; }

        public double DiscountAmount { get; set; }

        public double SubtotalAmount { get; set; }

        // =====================================
        // DADOS DO CLIENTE
        // =====================================

        [Required(ErrorMessage = "Informe o nome.")]
        [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o sobrenome.")]
        [StringLength(100, ErrorMessage = "O sobrenome deve ter no máximo 100 caracteres.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(200, ErrorMessage = "O e-mail deve ter no máximo 200 caracteres.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o celular.")]
        [RegularExpression(
            @"^\([1-9][0-9]\) 9[0-9]{4}-[0-9]{4}$",
            ErrorMessage = "Informe um celular válido com DDD. Exemplo: (19) 99999-9999.")]
        [StringLength(15, ErrorMessage = "O celular deve ter no máximo 11 dígitos.")]
        public string Telephone { get; set; } = string.Empty;

        // =====================================
        // OUTROS DADOS
        // =====================================

        public DateTime DateTime { get; set; } = DateTime.Now;

        public string CardNumber { get; set; } = string.Empty;

        public string CVV { get; set; } = string.Empty;

        public string ExpireMonthYear { get; set; } = string.Empty;

        public bool Demonstration { get; set; } = true;
    }
}
