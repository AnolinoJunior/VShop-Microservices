
namespace VShop.OrderApi.Events
{
    public class OrderCreatedEvent
    {
        public int OrderId { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }
}
