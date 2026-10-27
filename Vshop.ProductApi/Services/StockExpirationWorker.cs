namespace Vshop.ProductApi.Services;

public sealed class StockExpirationWorker(IServiceScopeFactory scopes, ILogger<StockExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<StockReservationService>().ExpireBatch(DateTime.UtcNow);
            }
            catch (Exception ex) { logger.LogError(ex, "Falha ao expirar reservas de estoque."); }
        }
    }
}
