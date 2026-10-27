namespace VShop.OrderApi.Models;

public enum OrderStatus
{
    Pending = 0,
    StockReserved = 1,
    CouponReserved = 2,
    PaymentApproved = 3,
    Confirmed = 4,
    Failed = 5,
    Cancelled = 6
}
