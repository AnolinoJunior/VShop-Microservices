namespace VShop.OrderApi.Models;

public class OrderHeader
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
    public string PaymentMethod { get; set; } = "Demonstration";
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
