using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using MediatR;

namespace Application.Features.Orders.Queries.GetUserOrderById;

public class GetUserOrderByIdQuery : IRequest<GetUserOrderByIdQueryResponse>, ICachableRequest
{
    public string Id { get; set; }
    public string CacheKey => $"GetUserOrderByIdQuery({Id})";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Orders";
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);

    public class GetUserOrderByIdQueryHandler : IRequestHandler<GetUserOrderByIdQuery, GetUserOrderByIdQueryResponse>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetUserOrderByIdQueryHandler(
            IOrderRepository orderRepository,
            IMapper mapper,
            IStorageService storageService)
        {
            _orderRepository = orderRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetUserOrderByIdQueryResponse> Handle(
            GetUserOrderByIdQuery request, 
            CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetUserOrderByIdAsync(request.Id);
            if (order == null)
                throw new Exception("Order not found");

            var response = _mapper.Map<GetUserOrderByIdQueryResponse>(order);

            // Her order item için showcase image'ı dönüştür
            foreach (var orderItemDto in response.OrderItems)
            {
                var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == orderItemDto.Id);
                if (orderItem?.Product?.ProductImageFiles == null) continue;

                var showcaseImage = orderItem.Product.ProductImageFiles.FirstOrDefault();
                if (showcaseImage != null)
                {
                    orderItemDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }
            }

            return response;
        }
    }
}