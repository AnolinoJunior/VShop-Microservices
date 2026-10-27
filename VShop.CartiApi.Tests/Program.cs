using VShop.CartiApi.Rules;

var cases = new[]
{
    (Subtotal: 49.99m, Expected: 0m),
    (Subtotal: 50.00m, Expected: 10m),
    (Subtotal: 50.01m, Expected: 10m)
};

foreach (var test in cases)
{
    var actual = CouponRules.CalculateDiscount(test.Subtotal, 10m);
    if (actual != test.Expected)
        throw new InvalidOperationException($"Subtotal {test.Subtotal:C}: esperado {test.Expected:C}, obtido {actual:C}.");
    Console.WriteLine($"OK: subtotal {test.Subtotal:C} -> desconto {actual:C}");
}

if (Environment.GetEnvironmentVariable("VSHOP_RESERVATION_TEST_SQL") is { Length: > 0 } connection)
    await VerifiedSubtotalTests.Run(connection);
