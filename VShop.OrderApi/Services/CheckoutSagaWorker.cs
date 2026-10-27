using Microsoft.EntityFrameworkCore;
using VShop.OrderApi.Context;
using VShop.OrderApi.Models;

namespace VShop.OrderApi.Services;

public sealed class CheckoutSagaWorker(IServiceScopeFactory scopes, ILogger<CheckoutSagaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var queryScope = scopes.CreateScope();
                var db = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ids = await db.CheckoutSagas.AsNoTracking().Where(s => s.Phase != SagaPhase.Completed && s.Phase != SagaPhase.Failed)
                    .OrderBy(s => s.UpdatedAtUtc).Select(s => s.OrderHeaderId).Take(20).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    using var scope = scopes.CreateScope();
                    try { await scope.ServiceProvider.GetRequiredService<CheckoutSagaService>().Process(id, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { logger.LogError(ex, "Falha ao recuperar pedido {OrderId}.", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Falha ao recuperar sagas de checkout."); }
        }
    }
}
