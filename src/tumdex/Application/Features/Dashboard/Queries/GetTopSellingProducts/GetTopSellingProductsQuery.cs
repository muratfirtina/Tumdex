using System.Linq.Expressions;
using Application.Features.Dashboard.Dtos;
using Application.Repositories;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries.GetTopSellingProducts;

public class GetTopSellingProductsQuery : IRequest<GetTopSellingProductsResponse>
{
    public string TimeFrame { get; set; } = "all";
    public int Count { get; set; } = 10;
    
    public class GetTopSellingProductsQueryHandler : IRequestHandler<GetTopSellingProductsQuery, GetTopSellingProductsResponse>
    {
        private readonly IOrderItemRepository _orderItemRepository;
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;

        public GetTopSellingProductsQueryHandler(
            IOrderItemRepository orderItemRepository,
            IProductRepository productRepository,
            IMapper mapper)
        {
            _orderItemRepository = orderItemRepository;
            _productRepository = productRepository;
            _mapper = mapper;
        }

        public async Task<GetTopSellingProductsResponse> Handle(GetTopSellingProductsQuery request, CancellationToken cancellationToken)
        {
            // İlk olarak en çok sipariş edilen ürünleri al
            var topOrderedProducts = await _orderItemRepository.GetMostOrderedProductsAsync(request.Count);
            
            // Ürün detaylarını al
            var productIds = topOrderedProducts.Select(p => p.ProductId).ToList();
            
            if (productIds.Count == 0)
            {
                return new GetTopSellingProductsResponse { Products = new List<TopProductDto>() };
            }
            
            // Ürünleri ve ilgili verileri yükle
            Expression<Func<Product, bool>> predicate = p => productIds.Contains(p.Id);
            var include = (IQueryable<Product> query) => query
                .Include(p => p.ProductImageFiles.Where(pif => pif.Showcase))
                .Include(p => p.Brand);
                
            var products = await _productRepository.GetAllAsync(
                predicate,
                include: include,
                enableTracking: false,
                cancellationToken: cancellationToken);

            // Sonuç DTO'ları oluştur
            var result = topOrderedProducts.Select(top => 
            {
                var product = products.FirstOrDefault(p => p.Id == top.ProductId);
                if (product == null) return null;
                
                return new TopProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Count = top.OrderCount,
                    Image = product.ProductImageFiles
                        .Where(pif => pif.Showcase)
                        .Select(pif => pif.Path)
                        .FirstOrDefault(),
                    Price = product.Price ?? 0,
                    BrandName = product.Brand?.Name
                };
            })
            .Where(p => p != null)
            .ToList();

            return new GetTopSellingProductsResponse { Products = result };
        }
    }
}