using Lamort.Shared.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Lamort.DocRender.Services;

public sealed class MortgageReadyForReviewConsumer(
    MortgagePdfRenderer renderer,
    IOptions<RabbitMqOptions> options,
    ILogger<MortgageReadyForReviewConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Value.HostName,
            UserName = options.Value.UserName,
            Password = options.Value.Password
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        RabbitMqTopology.ConfigureConsumer(channel, options.Value);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, eventArgs) =>
        {
            var message = RabbitMqTopology.Deserialize<MortgageApplicationReadyForReviewEvent>(eventArgs.Body);
            if (message is null)
            {
                channel.BasicAck(eventArgs.DeliveryTag, false);
                return;
            }

            try
            {
                await renderer.RenderAsync(message, stoppingToken);
                logger.LogInformation("Rendered mortgage PDF {MortgageApplicationId}", message.MortgageApplicationId);
                channel.BasicAck(eventArgs.DeliveryTag, false);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed rendering mortgage PDF {MortgageApplicationId}", message.MortgageApplicationId);
                channel.BasicNack(eventArgs.DeliveryTag, false, false);
            }
        };

        channel.BasicConsume(options.Value.QueueName, autoAck: false, consumer);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
