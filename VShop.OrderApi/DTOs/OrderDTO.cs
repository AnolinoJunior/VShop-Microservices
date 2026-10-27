using VShop.OrderApi.Models;

namespace VShop.OrderApi.DTOs;

public class OrderDTO
{
    public long OrderHeaderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? CouponCode { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public IReadOnlyCollection<OrderDetailDTO> Details { get; set; } = Array.Empty<OrderDetailDTO>();
}
