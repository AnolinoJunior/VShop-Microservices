
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace VShop.OrderApi.Services
{
    public class RabbitMqConsumer : BackgroundService
    {
        private readonly ILogger<RabbitMqConsumer> _logger;

        public RabbitMqConsumer(ILogger<RabbitMqConsumer> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            // Configuração da conexão com o RabbitMQ
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            // Criação da conexão
            await using var connection =
                await factory.CreateConnectionAsync(
                    cancellationToken: stoppingToken);

            // Criação do canal
            await using var channel =
                await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);

            // Declaração da fila
            await channel.QueueDeclareAsync(
                queue: "vshop.test.queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // Criação do Consumer
            var consumer = new AsyncEventingBasicConsumer(channel);

            // Evento executado quando uma mensagem é recebida
            consumer.ReceivedAsync += async (sender, eventArgs) =>
            {
                try
                {
                    // Obtém os bytes da mensagem
                    var body = eventArgs.Body.ToArray();

                    // Converte os bytes em texto
                    var mensagem = Encoding.UTF8.GetString(body);

                    // Registra a mensagem recebida
                    _logger.LogInformation(
                        "RABBITMQ - Mensagem recebida: {Mensagem}",
                        mensagem
                    );

                    // Confirma o processamento
                    await channel.BasicAckAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Erro ao processar mensagem do RabbitMQ."
                    );

                    // Não confirma a mensagem em caso de falha.
                    // O tratamento de rejeição e repetição será
                    // implementado posteriormente.
                }
            };

            // Inicia o consumo de mensagens
            await channel.BasicConsumeAsync(
                queue: "vshop.test.queue",
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation(
                "RABBITMQ - Consumer iniciado. Aguardando mensagens..."
            );

            // Mantém o Consumer ativo
            try
            {
                await Task.Delay(
                    Timeout.Infinite,
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "RABBITMQ - Consumer encerrado."
                );
            }
        }
    }
}
