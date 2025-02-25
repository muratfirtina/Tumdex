using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Orders.Queries.GetById;

public class GetOrderByIdQuery : IRequest<GetOrderByIdQueryResponse>,ICachableRequest
{
    public string Id { get; set; }

    public string CacheKey => $"GetOrderByIdQuery({Id})";
    public bool BypassCache { get; }
    public string? CacheGroupKey => "Orders";
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(5);

    public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, GetOrderByIdQueryResponse>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetOrderByIdQueryHandler(IOrderRepository orderRepository, IMapper mapper, IStorageService storageService)
        {
            _orderRepository = orderRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetOrderByIdQueryResponse> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
        {
            // Siparişi veritabanından getiriyoruz ve ilgili ilişkileri (OrderItems ve Product) yüklüyoruz
            var order = await _orderRepository.GetAsync(
                predicate: o => o.Id == request.Id,
                include: o => o
                    .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.ProductImageFiles)
                    .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.Brand)
                    .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.ProductFeatureValues).ThenInclude(pfv => pfv.FeatureValue).ThenInclude(fv => fv.Feature)
                    .Include(o => o.User) // Kullanıcı bilgilerini dahil ediyoruz
                    .Include(o => o.UserAddress)
                    .Include(o => o.PhoneNumber), // Adres bilgilerini dahil ediyoruz
                cancellationToken: cancellationToken
            );

            if (order == null)
                throw new Exception($"Sipariş bulunamadı: {request.Id}");

            // Order'dan GetOrderByIdQueryResponse DTO'suna dönüştürüyoruz
            var response = _mapper.Map<GetOrderByIdQueryResponse>(order);

            // Sipariş öğelerinin resim URL'lerini ayarlıyoruz (resim dosyalarını dinamik olarak alıyoruz)
            foreach (var orderItemDto in response.OrderItems)
            {
                var orderItem = order.OrderItems.First(oi => oi.Id == orderItemDto.Id);
                var showcaseImage = orderItem.Product.ProductImageFiles?.FirstOrDefault();
                if (showcaseImage != null)
                {
                    orderItemDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }
            }

            return response;
        }
    }
}