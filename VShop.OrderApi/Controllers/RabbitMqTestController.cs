
using Microsoft.AspNetCore.Mvc;
using VShop.OrderApi.Events;
using VShop.OrderApi.Services;

namespace VShop.OrderApi.Controllers
{
    [ApiController]
    [Route("api/rabbitmq-test")]
    public class RabbitMqTestController : ControllerBase
    {
        private readonly RabbitMqPublisher _publisher;

        public RabbitMqTestController(RabbitMqPublisher publisher)
        {
            _publisher = publisher;
        }

        [HttpPost("order-created")]
        public async Task<IActionResult> PublicarPedidoCriado()
        {
            var evento = new OrderCreatedEvent
            {
                OrderId = 999,
                Status = "Created",
                CreatedAtUtc = DateTime.UtcNow
            };

            await _publisher.PublicarPedidoCriadoAsync(evento);

            return Ok(new
            {
                Message = "Evento publicado no RabbitMQ!",
                Evento = evento
            });
        }
    }
}
