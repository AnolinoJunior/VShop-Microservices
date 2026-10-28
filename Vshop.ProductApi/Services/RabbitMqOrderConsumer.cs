
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Vshop.ProductApi.Services
{
    public class RabbitMqOrderConsumer : BackgroundService
    {
        private readonly ILogger<RabbitMqOrderConsumer> _logger;

        public RabbitMqOrderConsumer(
            ILogger<RabbitMqOrderConsumer> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            await using var connection =
                await factory.CreateConnectionAsync(
                    cancellationToken: stoppingToken);

            await using var channel =
                await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: "vshop.product.order-created",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, eventArgs) =>
            {
                try
                {
                    var body = eventArgs.Body.ToArray();

                    var mensagem = Encoding.UTF8.GetString(body);

                    _logger.LogInformation(
                        "PRODUCT API - Evento recebido: {Mensagem}",
                        mensagem);

                    await channel.BasicAckAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Erro ao consumir evento na ProductApi.");
                }
            };

            await channel.BasicConsumeAsync(
                queue: "vshop.product.order-created",
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "PRODUCT API - Consumer iniciado!");

            try
            {
                await Task.Delay(
                    Timeout.Infinite,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "PRODUCT API - Consumer encerrado.");
            }
        }
    }
}
