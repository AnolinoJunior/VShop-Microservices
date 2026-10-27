using System.ComponentModel.DataAnnotations;

namespace VShop.CartiApi.DTOs;

public class CheckoutHeaderDTO
{
    public string UserId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = string.Empty;
    [Required, Phone, StringLength(30)] public string Telephone { get; set; } = string.Empty;
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public bool Demonstration { get; set; } = true;
}
