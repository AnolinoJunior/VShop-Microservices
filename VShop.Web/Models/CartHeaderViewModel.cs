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
        [Required(ErrorMessage = "Informe o nome."), StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string FirstName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Informe o sobrenome."), StringLength(100, ErrorMessage = "O sobrenome deve ter no máximo 100 caracteres.")]
        public string LastName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido."), StringLength(200, ErrorMessage = "O e-mail deve ter no máximo 200 caracteres.")]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Informe o telefone."), Phone(ErrorMessage = "Informe um telefone válido."), StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
        public string Telephone { get; set; } = string.Empty;
        public DateTime DateTime { get; set; } = DateTime.Now;
        public string CardNumber { get; set; } = string.Empty;
        public string CVV { get; set; } = string.Empty;
        public string ExpireMonthYear { get; set; } = string.Empty;
        public bool Demonstration { get; set; } = true;
    }
}
