using Application.Consts;
using Application.Features.Orders.Dtos;
using Application.Repositories;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.OrderItems.Commands.Update;

public class UpdateOrderItemsCommand : IRequest<bool>, ITransactionalRequest, ICacheRemoverRequest
{
    public string OrderId { get; set; }
    public List<OrderItemUpdateDto> Items { get; set; }

    public string CacheKey => "";
    public bool BypassCache => false;
    public string CacheGroupKey => CacheGroups.Orders;

    public class UpdateOrderItemsCommandHandler : IRequestHandler<UpdateOrderItemsCommand, bool>
    {
        private readonly IOrderItemRepository _orderItemRepository;
        private readonly IOrderRepository _orderRepository;

        public UpdateOrderItemsCommandHandler(
            IOrderItemRepository orderItemRepository,
            IOrderRepository orderRepository)
        {
            _orderItemRepository = orderItemRepository;
            _orderRepository = orderRepository;
        }

        public async Task<bool> Handle(UpdateOrderItemsCommand request, CancellationToken cancellationToken)
        {
            // Tüm öğeler için tek bir işlem başlat
            foreach (var item in request.Items)
            {
                var orderItem = await _orderItemRepository.GetAsync(
                    x => x.Id == item.Id, 
                    cancellationToken: cancellationToken);
                
                if (orderItem == null) continue;

                orderItem.Quantity = item.Quantity;
                orderItem.Price = item.Price;
                orderItem.LeadTime = item.LeadTime;
                orderItem.UpdatedPrice = item.UpdatedPrice;

                await _orderItemRepository.UpdateAsync(orderItem);
            }

            // Sipariş toplam fiyatını güncelle
            var order = await _orderRepository.GetAsync(
                x => x.Id == request.OrderId,
                include: x => x.Include(o => o.OrderItems),
                cancellationToken: cancellationToken);
                
            if (order != null)
            {
                order.TotalPrice = order.OrderItems.Sum(item => 
                    (item.UpdatedPrice ?? item.Price) * item.Quantity);
                await _orderRepository.UpdateAsync(order);
            }

            return true;
        }
    }
}