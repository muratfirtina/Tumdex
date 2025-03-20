using Application.Abstraction.Services.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Messaging;

public class RabbitMqMessageBroker : IMessageBroker, IDisposable
{
    private readonly IBus _bus;
    private readonly ILogger<RabbitMqMessageBroker> _logger;
    private readonly SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);
    private bool _disposed = false;

    public RabbitMqMessageBroker(IBus bus, ILogger<RabbitMqMessageBroker> logger)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<T>(T message, string routingKey) where T : class
    {
        await EnsureConnectionAsync();
        
        try
        {
            await _connectionSemaphore.WaitAsync();
            
            _logger.LogInformation("Publishing message of type {MessageType} with routing key {RoutingKey}", 
                typeof(T).Name, routingKey);
                
            await _bus.Publish(message);
                
            _logger.LogInformation("Message of type {MessageType} published successfully", typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message of type {MessageType}", typeof(T).Name);
            throw;
        }
        finally
        {
            if (!_disposed)
            {
                _connectionSemaphore.Release();
            }
        }
    }

    public async Task SendAsync<T>(T message, string queueName) where T : class
    {
        await EnsureConnectionAsync();
        
        try
        {
            await _connectionSemaphore.WaitAsync();
            
            _logger.LogInformation("Sending message of type {MessageType} to queue {QueueName}", 
                typeof(T).Name, queueName);
                
            var sendEndpoint = await _bus.GetSendEndpoint(new Uri($"rabbitmq://localhost/{queueName}"));
            await sendEndpoint.Send(message);
                
            _logger.LogInformation("Message of type {MessageType} sent to queue {QueueName} successfully", 
                typeof(T).Name, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message of type {MessageType} to queue {QueueName}", 
                typeof(T).Name, queueName);
            throw;
        }
        finally
        {
            if (!_disposed)
            {
                _connectionSemaphore.Release();
            }
        }
    }
    
    private async Task EnsureConnectionAsync()
    {
        // RabbitMQ bağlantı durumunu kontrol etme ve yeniden deneme mantığı
        int retryCount = 0;
        const int maxRetries = 3;
        TimeSpan retryDelay = TimeSpan.FromSeconds(1);
        
        while (retryCount < maxRetries)
        {
            try
            {
                // Bağlantıyı kontrol et
                if (_bus.Address != null)
                {
                    return; // Bağlantı zaten var
                }
                
                // Bağlantı yok, bekleme ve yeniden deneme yapılacak
                await Task.Delay(retryDelay);
                retryDelay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                retryCount++;
            }
            catch
            {
                // Bekleme ve yeniden deneme yapılacak
                await Task.Delay(retryDelay);
                retryDelay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                retryCount++;
            }
        }
        
        _logger.LogWarning("Failed to ensure RabbitMQ connection after {MaxRetries} attempts", maxRetries);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _connectionSemaphore.Dispose();
            }
            
            _disposed = true;
        }
    }
}