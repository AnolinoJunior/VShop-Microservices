namespace VShop.DiscountApi.Models;

public enum CouponReservationStatus { Pending, Confirmed, Released, Expired, Prepared }

public class CouponReservation
{
    public Guid Id { get; set; }
    public int CouponId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool ManagedBySaga { get; set; }
    public decimal DiscountAmount { get; set; }
    public CouponReservationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
