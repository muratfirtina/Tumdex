using System.Text.Json;
using Application.Events.OrderEvetns;
using Application.Repositories;
using Domain;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Persistence.BackgroundJob;

public class OutboxProcessor : BackgroundService
{
   private readonly IServiceScopeFactory _scopeFactory;
   private readonly ILogger<OutboxProcessor> _logger;

   public OutboxProcessor(
       IServiceScopeFactory scopeFactory,
       ILogger<OutboxProcessor> logger)
   {
       _scopeFactory = scopeFactory;
       _logger = logger;
   }

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
       try
       {
           while (!stoppingToken.IsCancellationRequested)
           {
               try
               {
                   await ProcessMessages(stoppingToken);
               }
               catch (Exception ex) when (ex is not OperationCanceledException)
               {
                   _logger.LogError(ex, "Error in outbox processor");
               }

               await Task.Delay(GetProcessingInterval(), stoppingToken);
           }
       }
       catch (OperationCanceledException)
       {
           _logger.LogInformation("Outbox processor is shutting down gracefully...");
       }
   }

   private async Task ProcessMessages(CancellationToken stoppingToken)
   {
       using var scope = _scopeFactory.CreateScope();
       var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
       var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

       var messages = await outboxRepository.GetUnprocessedMessagesAsync();
       foreach (var message in messages)
       {
           try
           {
               switch (message.Type)
               {
                   case nameof(OrderCreatedEvent):
                       await ProcessEvent<OrderCreatedEvent>(message, publishEndpoint, stoppingToken);
                       break;
                   case nameof(OrderUpdatedEvent):
                       await ProcessEvent<OrderUpdatedEvent>(message, publishEndpoint, stoppingToken);
                       break;
                   case nameof(CartUpdatedEvent):
                       await ProcessEvent<CartUpdatedEvent>(message, publishEndpoint, stoppingToken);
                       break;
                   case nameof(StockUpdatedEvent):
                       await ProcessEvent<StockUpdatedEvent>(message, publishEndpoint, stoppingToken);
                       break;
                   default:
                       await HandleUnknownMessageType(message, outboxRepository);
                       continue;
               }

               await outboxRepository.MarkAsProcessedAsync(message.Id);
               _logger.LogInformation(
                   "Successfully processed message {MessageId} of type {Type}",
                   message.Id, message.Type);
           }
           catch (Exception ex) when (ex is not OperationCanceledException)
           {
               await HandleMessageError(message, ex, outboxRepository);
           }
       }
   }

   private async Task ProcessEvent<T>(OutboxMessage message, IPublishEndpoint publishEndpoint,
       CancellationToken cancellationToken) where T : class
   {
       try
       {
           var @event = JsonSerializer.Deserialize<T>(message.Data);
           if (@event != null)
           {
               await publishEndpoint.Publish(@event, cancellationToken);
               _logger.LogInformation(
                   "Event published successfully. Type: {EventType}, MessageId: {MessageId}",
                   typeof(T).Name, message.Id);
           }
           else
           {
               throw new InvalidOperationException($"Failed to deserialize event data for message {message.Id}");
           }
       }
       catch (JsonException ex)
       {
           _logger.LogError(ex,
               "JSON deserialization error for message {MessageId} of type {EventType}",
               message.Id, typeof(T).Name);
           throw;
       }
       catch (Exception ex)
       {
           _logger.LogError(ex,
               "Error processing event for message {MessageId} of type {EventType}",
               message.Id, typeof(T).Name);
           throw;
       }
   }

   private async Task HandleUnknownMessageType(OutboxMessage message, IOutboxRepository repository)
   {
       _logger.LogWarning(
           "Unknown message type: {Type} for message {MessageId}",
           message.Type, message.Id);
       await repository.MarkAsFailedAsync(message.Id, "Unknown message type");
   }

   private async Task HandleMessageError(OutboxMessage message, Exception ex, IOutboxRepository repository)
   {
       _logger.LogError(ex, 
           "Error processing message {MessageId}. Retry count: {RetryCount}", 
           message.Id, message.RetryCount);
       
       await repository.UpdateRetryCountAsync(message.Id, ex.Message);
   }

   private TimeSpan GetProcessingInterval()
   {
       // Varsayılan olarak 10 saniye, bu değer konfigürasyondan da alınabilir
       return TimeSpan.FromSeconds(10);
   }
}