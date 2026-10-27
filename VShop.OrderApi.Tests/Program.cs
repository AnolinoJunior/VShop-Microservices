using VShop.OrderApi.Services;

IPaymentService payment = new SimulatedPaymentService();
var passed = 0;

foreach (var decision in new[] { SimulatedPaymentDecision.Approve, SimulatedPaymentDecision.Decline })
{
    var result = await payment.AuthorizeAsync(1, 50m, decision);
    if (result != decision) throw new Exception($"Resultado inesperado para {decision}.");
    Console.WriteLine($"PASSOU: pagamento simulado {decision}");
    passed++;
}

try
{
    await payment.AuthorizeAsync(1, -1m, SimulatedPaymentDecision.Approve);
    throw new Exception("Valor negativo foi aceito.");
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine("PASSOU: valor negativo é rejeitado");
    passed++;
}

Console.WriteLine($"RESULTADO: {passed} passaram; 0 falharam.");

if (Environment.GetEnvironmentVariable("VSHOP_RESERVATION_TEST_SQL") is { Length: > 0 } connection)
{
    await OrderIntegrationTests.Run(connection);
    await SagaRecoveryTests.Run(connection);
}
