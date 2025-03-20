namespace Application.Abstraction.Services.Messaging;

public interface IMessageBroker
{
    Task PublishAsync<T>(T message, string routingKey) where T : class;
    Task SendAsync<T>(T message, string queueName) where T : class;
}