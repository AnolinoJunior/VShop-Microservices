namespace VShop.DiscountApi.DTOs;

public class CouponDTO
{
    public int CouponId { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public bool IsActive { get; set; }
}
