using Application.Abstraction.Services;
using Application.Abstraction.Services.HubServices;
using Application.Events.OrderEvetns;
using Application.Events.OrderEvetns;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Consumers;

public class OrderUpdatedEventConsumer : IConsumer<OrderUpdatedEvent>
{
    private readonly IMailService _mailService;
    private readonly IOrderHubService _orderHubService;
    private readonly ILogger<OrderUpdatedEventConsumer> _logger;

    public OrderUpdatedEventConsumer(
        IMailService mailService,
        IOrderHubService orderHubService,
        ILogger<OrderUpdatedEventConsumer> logger)
    {
        _mailService = mailService;
        _orderHubService = orderHubService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderUpdatedEvent> context)
    {
        var order = context.Message;
        _logger.LogInformation("Order updated event received: {OrderId}", order.OrderId);

        try
        {
            // Mail gönderimi
            try
            {
                await _mailService.SendOrderUpdateNotificationAsync(
                    order.Email,
                    order.OrderCode,
                    order.AdminNote,
                    order.OriginalStatus,
                    order.UpdatedStatus,
                    order.OriginalTotalPrice,
                    order.UpdatedTotalPrice,
                    order.UpdatedItems
                );
                _logger.LogInformation("Order update email sent: {OrderId}", order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order update email: {OrderId}", order.OrderId);
                // Mail hatası kritik değil, devam et
            }

            // SignalR bildirimi
            try
            {
                await _orderHubService.OrderUpdatedMessageAsync(order.OrderId, 
                    $"Sipariş durumu güncellendi: {order.OriginalStatus} -> {order.UpdatedStatus}");
                _logger.LogInformation("SignalR notification sent: {OrderId}", order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SignalR notification: {OrderId}", order.OrderId);
                // SignalR hatası kritik değil, devam et
            }

            _logger.LogInformation("Order update {OrderId} processing completed", order.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error processing order update {OrderId}", order.OrderId);
            throw; // Sadece kritik hatalarda retry
        }
    }
}