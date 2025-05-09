using Application.Consts;
using Application.Repositories;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using MediatR;

namespace Application.Features.Orders.Commands.Delete;

public class DeleteOrderCommand : IRequest<DeletedOrderCommandResponse>,ICacheRemoverRequest
{
    public string Id { get; set; }
    public string CacheKey => $"Order-{Id}"; // Spesifik sipariş önbelleğini sil
    public bool BypassCache => false;
    // Hem ilgili kullanıcının siparişlerini hem de genel admin sipariş listesini temizle.
    public string? CacheGroupKey => $"{CacheGroups.UserOrders},{CacheGroups.Orders}";
    public class DeleteOrderCommandHandler : IRequestHandler<DeleteOrderCommand, DeletedOrderCommandResponse>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IMapper _mapper;

        public DeleteOrderCommandHandler(IOrderRepository orderRepository, IMapper mapper)
        {
            _orderRepository = orderRepository;
            _mapper = mapper;
        }

        public async Task<DeletedOrderCommandResponse> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
            if (order == null) throw new Exception("Order not found");

            await _orderRepository.DeleteAsync(order);
            DeletedOrderCommandResponse response = _mapper.Map<DeletedOrderCommandResponse>(order);
            response.Success = true;
            return response;
        }
    }
}