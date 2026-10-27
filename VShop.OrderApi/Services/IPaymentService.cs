namespace VShop.OrderApi.Services;

public enum SimulatedPaymentDecision { Approve, Decline }

public interface IPaymentService
{
    Task<SimulatedPaymentDecision> AuthorizeAsync(
        long orderId, decimal amount, SimulatedPaymentDecision decision, CancellationToken cancellationToken = default);
}
