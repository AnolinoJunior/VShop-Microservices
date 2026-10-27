namespace VShop.DiscountApi.Services;

public sealed class CouponExpirationWorker(IServiceScopeFactory scopes, ILogger<CouponExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CouponReservationService>().ExpireBatch(DateTime.UtcNow);
            }
            catch (Exception ex) { logger.LogError(ex, "Falha ao expirar reservas de cupons."); }
        }
    }
}
