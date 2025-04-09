using System.Linq.Expressions;
using Application.Features.Dashboard.Dtos;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries.GetTopCartProducts;

public class GetTopCartProductsQuery : IRequest<GetTopCartProductsResponse>
{
    public string TimeFrame { get; set; } = "all";
    public int Count { get; set; } = 10;
    
    public class GetTopCartProductsQueryHandler : IRequestHandler<GetTopCartProductsQuery, GetTopCartProductsResponse>
    {
        private readonly ICartItemRepository _cartItemRepository;
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetTopCartProductsQueryHandler(
            ICartItemRepository cartItemRepository,
            IProductRepository productRepository,
            IMapper mapper,
            IStorageService storageService)
        {
            _cartItemRepository = cartItemRepository;
            _productRepository = productRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetTopCartProductsResponse> Handle(GetTopCartProductsQuery request, CancellationToken cancellationToken)
        {
            DateTime? startDate = GetStartDateFromTimeFrame(request.TimeFrame);

            // Sepete en çok eklenen ürünleri al
            var mostAddedProducts = await _cartItemRepository.GetMostAddedToCartProductsAsync(request.Count, startDate);
            
            // Ürün detaylarını al
            var productIds = mostAddedProducts.Select(p => p.ProductId).ToList();
            
            if (productIds.Count == 0)
            {
                return new GetTopCartProductsResponse { Products = new List<TopProductDto>() };
            }
            
            // Ürünleri ve ilgili verileri yükle
            Expression<Func<Product, bool>> predicate = p => productIds.Contains(p.Id);
            var include = (IQueryable<Product> query) => query
                .Include(p => p.ProductImageFiles)
                .Include(p => p.Brand);
                
            var products = await _productRepository.GetAllAsync(
                predicate,
                include: include,
                enableTracking: false,
                cancellationToken: cancellationToken);

            // Sonuç DTO'ları oluştur
            var result = mostAddedProducts.Select(top => 
            {
                var product = products.FirstOrDefault(p => p.Id == top.ProductId);
                if (product == null) return null;
                
                var showcaseImage = product.ProductImageFiles
                    .FirstOrDefault(pif => pif.Showcase) ?? 
                    product.ProductImageFiles.FirstOrDefault();
                
                var dto = new TopProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Count = top.Count,
                    Price = product.Price ?? 0,
                    BrandName = product.Brand?.Name
                };
                
                if (showcaseImage != null)
                {
                    dto.Image = showcaseImage.ToBaseDto(_storageService);
                }
                
                return dto;
            })
            .Where(p => p != null)
            .ToList();

            return new GetTopCartProductsResponse { Products = result };
        }

        private DateTime? GetStartDateFromTimeFrame(string timeFrame)
        {
            DateTime now = DateTime.UtcNow;
            
            return timeFrame switch
            {
                "day" => now.AddDays(-1),
                "week" => now.AddDays(-7),
                "month" => now.AddMonths(-1),
                "days10" => now.AddDays(-10),
                "days30" => now.AddDays(-30),
                _ => null, // "all" or any other value returns null (no time filtering)
            };
        }
    }
}