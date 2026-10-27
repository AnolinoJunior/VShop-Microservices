namespace VShop.CartiApi.Rules;

public static class CouponRules
{
    public const decimal MinimumSubtotal = 50m;

    public static decimal CalculateDiscount(decimal subtotal, decimal couponDiscount) =>
        subtotal >= MinimumSubtotal ? Math.Min(subtotal, Math.Max(0m, couponDiscount)) : 0m;
}
