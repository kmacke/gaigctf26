using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Lamort.Shared.Messaging;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "rabbitmq";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "lamort.events";
    public string QueueName { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = "message.created";
}

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);
}

public sealed class RabbitMqPublisher(RabbitMqOptions options) : IMessagePublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            UserName = options.UserName,
            Password = options.Password
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        RabbitMqTopology.ConfigurePublisher(channel, options);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        channel.BasicPublish(options.ExchangeName, options.RoutingKey, properties, payload);
        return Task.CompletedTask;
    }
}

public static class RabbitMqTopology
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static void ConfigurePublisher(IModel channel, RabbitMqOptions options)
    {
        channel.ExchangeDeclare(options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
    }

    public static void ConfigureConsumer(IModel channel, RabbitMqOptions options)
    {
        ConfigurePublisher(channel, options);
        channel.QueueDeclare(options.QueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(options.QueueName, options.ExchangeName, options.RoutingKey);
    }

    public static T? Deserialize<T>(ReadOnlyMemory<byte> body)
    {
        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(body.Span), SerializerOptions);
    }
}
