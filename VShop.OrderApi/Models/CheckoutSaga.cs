namespace VShop.OrderApi.Models;

public enum SagaPhase { Reserving, Paying, Preparing, Committing, Compensating, Completed, Failed, Reconciliation }

public sealed class CheckoutSaga
{
    public long OrderHeaderId { get; set; }
    public OrderHeader Order { get; set; } = null!;
    public SagaPhase Phase { get; set; }
    public bool SimulateDecline { get; set; }
    public bool CommitDecision { get; set; }
    public Guid? CouponReservationId { get; set; }
    public bool CouponReserved { get; set; }
    public bool CouponPrepared { get; set; }
    public bool CouponConfirmed { get; set; }
    public bool CouponReleased { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ICollection<CheckoutSagaItem> Items { get; set; } = new List<CheckoutSagaItem>();
}

public sealed class CheckoutSagaItem
{
    public long OrderDetailId { get; set; }
    public OrderDetail OrderDetail { get; set; } = null!;
    public long OrderHeaderId { get; set; }
    public CheckoutSaga Saga { get; set; } = null!;
    public Guid ReservationId { get; set; }
    public bool Reserved { get; set; }
    public bool Prepared { get; set; }
    public bool Confirmed { get; set; }
    public bool Released { get; set; }
}
