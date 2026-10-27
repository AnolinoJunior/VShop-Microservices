using System.ComponentModel.DataAnnotations;

namespace VShop.Web.Models;

public sealed class PrepareOrderRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = string.Empty;
    [Required, Phone, StringLength(30)] public string Telephone { get; set; } = string.Empty;
    [StringLength(300)] public string? Address { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}
