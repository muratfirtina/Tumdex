using Application.Repositories;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Consts;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;

namespace Application.Features.OrderItems.Commands.Delete
{
    public class DeleteOrderItemCommand : IRequest<bool>,ITransactionalRequest,ICacheRemoverRequest
    {
        public string Id { get; set; }  // Silinecek OrderItem ID'si

        public string CacheKey => ""; // Spesifik order item silme, grup bazlı temizleme yeterli.
        public bool BypassCache => false;
        public string? CacheGroupKey => CacheGroups.UserOrders;

        public class DeleteOrderItemCommandHandler : IRequestHandler<DeleteOrderItemCommand, bool>
        {
            private readonly IOrderItemRepository _orderItemRepository;

            public DeleteOrderItemCommandHandler(IOrderItemRepository orderItemRepository)
            {
                _orderItemRepository = orderItemRepository;
            }

            public async Task<bool> Handle(DeleteOrderItemCommand request, CancellationToken cancellationToken)
            {
                // OrderItem'ı silmek için repository metodunu kullan
                var result = await _orderItemRepository.RemoveOrderItemAsync(request.Id);

                // İşlem başarılıysa true, başarısızsa false döner
                return result;
            }
        }
    }
}