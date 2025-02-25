using Application.Abstraction.Services;
using Application.Abstraction.Services.HubServices;
using Application.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SignalR.Hubs;

namespace SignalR.HubService;

public class OrderHubService : IOrderHubService
{
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<OrderHubService> _logger;
    private readonly IOrderRepository _orderRepository;

    public OrderHubService(IHubContext<OrderHub> hubContext, ILogger<OrderHubService> logger,
        IOrderRepository orderRepository)
    {
        _hubContext = hubContext;
        _logger = logger;
        _orderRepository = orderRepository;
    }

    public async Task OrderCreatedMessageAsync(string orderId, string message)
    {
        try
        {
            // Sipariş bilgilerini al
            var order = await _orderRepository.GetAsync(
                predicate: o => o.Id == orderId,
                include: x => x
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product).ThenInclude(p => p.Brand)
                    .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product).ThenInclude(p => p.ProductFeatureValues)
                    .ThenInclude(pfv => pfv.FeatureValue)
                    .ThenInclude(fv => fv.Feature)
            );

            if (order == null)
            {
                _logger.LogWarning("Order not found for notification. OrderId: {OrderId}", orderId);
                return;
            }

            var notification = new
            {
                Type = "OrderCreated",
                OrderId = orderId,
                OrderNumber = order.OrderCode,
                Message = message,
                CustomerName = order.User?.UserName ?? "Belirtilmemiş",
                TotalAmount = order.TotalPrice,
                Items = order.OrderItems?.Select(item => new
                {
                    ProductName = item.Product.Name,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    BrandName = item.Product.Brand?.Name,
                    Features = item.Product.ProductFeatureValues.Select(pfv => new
                    {
                        FeatureName = pfv.FeatureValue.Feature?.Name,
                        ValueName = pfv.FeatureValue.Name
                    }).ToList()
                }).ToList(),
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group("Admins")
                .SendAsync(ReceiveFunctionNames.ReceiveOrderCreated, notification);

            _logger.LogInformation(
                "Order creation notification sent. OrderId: {OrderId}", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending order creation notification. OrderId: {OrderId}", orderId);
            throw;
        }
    }

    public async Task OrderStausChangedMessageAsync(string orderId, string status, string message)
    {
        try
        {
            var order = await _orderRepository.GetAsync(
                predicate: o => o.Id == orderId,
                include: x => x
                    .Include(o => o.User)
            );

            if (order == null)
            {
                _logger.LogWarning("Order not found for status update notification. OrderId: {OrderId}", orderId);
                return;
            }

            var notification = new
            {
                Type = "OrderStatusUpdate",
                OrderId = orderId,
                OrderNumber = order.OrderCode,
                Message = message,
                CustomerName = order.User?.UserName ?? "Belirtilmemiş",
                Status = status,
                PreviousStatus = order.Status?.ToString(),
                UpdatedBy = order.LastModifiedBy,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group("Admins")
                .SendAsync(ReceiveFunctionNames.ReceiveOrderStatusUpdate, notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending order status update notification. OrderId: {OrderId}", orderId);
            throw;
        }
    }

    public async Task OrderUpdatedMessageAsync(string orderId, string message)
    {
        try
        {
            // Güncel ve önceki değerleri karşılaştırmak için siparişi al
            var order = await _orderRepository.GetAsync(
                predicate: o => o.Id == orderId,
                include: x => x
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Brand)
                    .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.ProductFeatureValues)
                    .ThenInclude(pfv => pfv.FeatureValue)
                    .ThenInclude(fv => fv.Feature),
                enableTracking: false
            );

            if (order == null)
            {
                _logger.LogWarning("Order not found for update notification. OrderId: {OrderId}", orderId);
                return;
            }

            // Değişikliklerin detaylarını topla
            var orderChanges = await _orderRepository.GetChanges(orderId);

            var notification = new
            {
                Type = "OrderUpdated",
                OrderId = orderId,
                OrderNumber = order.OrderCode,
                Message = message,
                CustomerName = order.User?.UserName ?? "Belirtilmemiş",
                Status = new
                {
                    Previous = orderChanges.PreviousStatus?.ToString(),
                    Current = order.Status.ToString(),
                    Changed = orderChanges.PreviousStatus != order.Status
                },
                Items = order.OrderItems.Select(item =>
                {
                    var previousItem = orderChanges.PreviousItems
                        .FirstOrDefault(pi => pi.Id == item.Id);

                    return new
                    {
                        Id = item.Id,
                        ProductName = item.Product.Name,
                        BrandName = item.Product.Brand?.Name,
                        FeatureValues = item.Product.ProductFeatureValues.Select(pfv => new
                        {
                            FeatureName = pfv.FeatureValue.Feature?.Name,
                            ValueName = pfv.FeatureValue.Name
                        }).ToList(),
                        Quantity = new
                        {
                            Previous = previousItem?.Quantity,
                            Current = item.Quantity,
                            Changed = previousItem?.Quantity != item.Quantity
                        },
                        Price = new
                        {
                            Previous = item.Price,
                            Current = item.UpdatedPrice ?? item.Price,
                            Changed = item.UpdatedPrice.HasValue && item.UpdatedPrice != item.Price
                        },
                        LeadTime = new
                        {
                            Previous = previousItem?.LeadTime,
                            Current = item.LeadTime,
                            Changed = previousItem?.LeadTime != item.LeadTime
                        }
                    };
                }).ToList(),
                TotalAmount = new
                {
                    Previous = orderChanges.PreviousTotalPrice,
                    Current = order.TotalPrice,
                    Changed = orderChanges.PreviousTotalPrice != order.TotalPrice
                },
                AdminNote = new
                {
                    Previous = orderChanges.PreviousAdminNote,
                    Current = order.AdminNote,
                    Changed = orderChanges.PreviousAdminNote != order.AdminNote
                },
                UpdatedBy = order.LastModifiedBy,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group("Admins")
                .SendAsync(ReceiveFunctionNames.ReceiveOrderUpdated, notification);

            _logger.LogInformation(
                "Order update notification sent. OrderId: {OrderId}, Changes: {@Changes}",
                orderId, notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending order update notification. OrderId: {OrderId}", orderId);
            throw;
        }
    }
}