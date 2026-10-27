namespace VShop.OrderApi.Services;

public sealed class SimulatedPaymentService : IPaymentService
{
    public Task<SimulatedPaymentDecision> AuthorizeAsync(
        long orderId, decimal amount, SimulatedPaymentDecision decision, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision));
        return Task.FromResult(decision);
    }
}
