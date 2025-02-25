using System.Text.Json;
using Application.Abstraction.Services.HubServices;
using Application.Events.OrderEvetns;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Orders.Dtos;
using Application.Repositories;
using Application.Storage;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain;
using Domain.Enum;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Orders.Commands.Update;

public class UpdateOrderCommand : IRequest<bool>, ITransactionalRequest, ICacheRemoverRequest
{
    public string Id { get; set; }
    public OrderStatus? Status { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? AdminNote { get; set; }
    public List<OrderItemUpdateDto>? UpdatedItems { get; set; }

    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => "Orders";

    public class UpdateOrderCommandHandler : IRequestHandler<UpdateOrderCommand, bool>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOutboxRepository _outboxRepository;
        private readonly IStorageService _storageService;
        private readonly ILogger<UpdateOrderCommandHandler> _logger;
        private readonly IOrderHubService _orderHubService; // Ekle

        public UpdateOrderCommandHandler(
            IOrderRepository orderRepository,
            IOutboxRepository outboxRepository,
            IStorageService storageService,
            ILogger<UpdateOrderCommandHandler> logger,
            IOrderHubService orderHubService) // Constructor'a ekle
        {
            _orderRepository = orderRepository;
            _outboxRepository = outboxRepository;
            _storageService = storageService;
            _logger = logger;
            _orderHubService = orderHubService;
        }

        public async Task<bool> Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetAsync(
                x => x.Id == request.Id,
                include: x => x.Include(o => o.User)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Brand)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImageFiles),
                cancellationToken: cancellationToken
            );

            if (order == null) throw new Exception("Order not found");

            var originalStatus = order.Status;
            var originalTotalPrice = order.TotalPrice;
            var updatedItems = request.UpdatedItems ?? CreateUpdatedItems(order.OrderItems.ToList());

            // Fiyat güncelleme
            var updatedTotalPrice = request.TotalPrice.HasValue
                ? updatedItems.Where(x => x.UpdatedPrice.HasValue).Sum(x => x.UpdatedPrice.Value * x.Quantity)
                : order.TotalPrice;

            // Status değişimi varsa bildirimi gönder
            /*if (request.Status.HasValue && request.Status.Value != originalStatus)
            {
                await _orderHubService.OrderStausChangedMessageAsync(
                    order.Id,
                    request.Status.Value.ToString(),
                    $"Sipariş durumu {originalStatus} -> {request.Status.Value} olarak güncellendi."
                );
            }*/

            // Order güncelleme
            if (request.Status.HasValue)
                order.Status = request.Status.Value;

            order.TotalPrice = updatedTotalPrice;
            order.AdminNote = request.AdminNote;

            // OrderItems güncelleme
            bool itemsUpdated = false;
            if (request.UpdatedItems?.Any() ?? false)
            {
                foreach (var orderItem in order.OrderItems)
                {
                    var updatedItem = request.UpdatedItems.FirstOrDefault(ui => ui.Id == orderItem.Id);
                    if (updatedItem != null)
                    {
                        orderItem.Price = updatedItem.UpdatedPrice ?? orderItem.Price;
                        orderItem.LeadTime = updatedItem.LeadTime;
                        itemsUpdated = true;
                    }
                }
            }
            else
            {
                foreach (var item in order.OrderItems.Where(item => item.Product != null))
                {
                    var originalPrice = item.Price;
                    item.Price = item.Product.Price;
                    item.LeadTime = item.LeadTime;
                    
                    if (originalPrice != item.Price)
                        itemsUpdated = true;
                }
            }

            // Event oluşturma
            var orderUpdatedEvent = new OrderUpdatedEvent
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Email = order.User.Email,
                OriginalStatus = originalStatus,
                UpdatedStatus = order.Status,
                OriginalTotalPrice = originalTotalPrice,
                UpdatedTotalPrice = updatedTotalPrice,
                AdminNote = order.AdminNote,
                UpdatedItems = updatedItems
            };

            // Sipariş güncellemesi bildirimi gönder
            /*if (itemsUpdated || originalTotalPrice != updatedTotalPrice)
            {
                var updateMessage = $"Sipariş güncellendi. " +
                    (originalTotalPrice != updatedTotalPrice 
                        ? $"Toplam tutar: {originalTotalPrice:C2} -> {updatedTotalPrice:C2}" 
                        : "");
                
                await _orderHubService.OrderUpdatedMessageAsync(order.Id, updateMessage);
            }*/

            // Outbox mesajı oluşturma
            var outboxMessage = new OutboxMessage(
                nameof(OrderUpdatedEvent),
                JsonSerializer.Serialize(orderUpdatedEvent)
            );

            // Order güncelleme ve outbox mesajı kaydetme işlemleri
            await _orderRepository.UpdateAsync(order);
            await _outboxRepository.AddAsync(outboxMessage);

            _logger.LogInformation(
                "Order {OrderId} updated. Status: {OldStatus} -> {NewStatus}, Price: {OldPrice} -> {NewPrice}",
                order.Id, originalStatus, order.Status, originalTotalPrice, updatedTotalPrice);

            return true;
        }

        private List<OrderItemUpdateDto> CreateUpdatedItems(List<OrderItem> orderItems)
        {
            var updatedItems = new List<OrderItemUpdateDto>();

            foreach (var item in orderItems)
            {
                var updatedItem = new OrderItemUpdateDto
                {
                    Id = item.Id,
                    ProductName = item.ProductName,
                    BrandName = item.Product?.Brand?.Name ?? item.BrandName,
                    Price = item.Price,
                    UpdatedPrice = item.UpdatedPrice ?? item.Price,
                    Quantity = item.Quantity,
                    LeadTime = item.LeadTime,
                    ShowcaseImage = item.Product?.ProductImageFiles?.FirstOrDefault()?.ToDto(_storageService)
                };
                updatedItems.Add(updatedItem);
            }

            return updatedItems;
        }
    }
}