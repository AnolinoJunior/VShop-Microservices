namespace Vshop.ProductApi.Models;

public enum ReservationStatus { Pending, Confirmed, Released, Expired, Prepared }

public class StockReservation
{
    public Guid Id { get; set; }
    public int ProductId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long Quantity { get; set; }
    public bool ManagedBySaga { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
