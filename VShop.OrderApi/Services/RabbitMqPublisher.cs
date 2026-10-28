
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using VShop.OrderApi.Events;

namespace VShop.OrderApi.Services
{
    public class RabbitMqPublisher
    {
        public async Task PublicarPedidoCriadoAsync(
            OrderCreatedEvent evento)
        {
            // Configuração da conexão
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            // Criação da conexão e do canal
            await using var connection =
                await factory.CreateConnectionAsync();

            await using var channel =
                await connection.CreateChannelAsync();

            // Declaração da fila da ProductApi
            await channel.QueueDeclareAsync(
                queue: "vshop.product.order-created",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Converte o evento para JSON
            var json = JsonSerializer.Serialize(evento);

            // Converte o JSON para bytes
            var body = Encoding.UTF8.GetBytes(json);

            // Configura as propriedades da mensagem
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            // Publica o evento no RabbitMQ
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "vshop.product.order-created",
                mandatory: false,
                basicProperties: properties,
                body: body
            );
        }
    }
}
