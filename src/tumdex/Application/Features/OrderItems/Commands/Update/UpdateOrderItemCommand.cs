using Application.Repositories;
using Domain;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Application.Pipelines.Transaction;

namespace Application.Features.OrderItems.Commands.Update
{
    public class UpdateOrderItemCommand : IRequest<bool>,ITransactionalRequest
    {
        public string Id { get; set; }  // OrderItem ID
        public int Quantity { get; set; }  // Güncellenecek miktar
        public decimal? Price { get; set; }  // Guncellenecek fiyat
        public int? LeadTime { get; set; }  // Güncellenecek lead time
        public decimal? UpdatedPrice { get; set; }  // Yeni fiyat

        public class UpdateOrderItemCommandHandler : IRequestHandler<UpdateOrderItemCommand, bool>
        {
            private readonly IOrderItemRepository _orderItemRepository;

            public UpdateOrderItemCommandHandler(IOrderItemRepository orderItemRepository)
            {
                _orderItemRepository = orderItemRepository;
            }

            public async Task<bool> Handle(UpdateOrderItemCommand request, CancellationToken cancellationToken)
            {
                
                var orderItem = await _orderItemRepository.GetAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
                if (orderItem == null) throw new Exception("Order Item not found");

                orderItem.Quantity = request.Quantity;
                orderItem.Price = request.Price;
                orderItem.LeadTime = request.LeadTime;
                orderItem.UpdatedPrice = request.UpdatedPrice;

                await _orderItemRepository.UpdateAsync(orderItem);
                return true;
                
            }
        }
    }
}