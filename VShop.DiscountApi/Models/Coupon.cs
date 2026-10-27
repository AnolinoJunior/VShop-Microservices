namespace VShop.DiscountApi.Models;

public class Coupon
{
    public int CouponId { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public bool IsActive { get; set; } = true;
}
