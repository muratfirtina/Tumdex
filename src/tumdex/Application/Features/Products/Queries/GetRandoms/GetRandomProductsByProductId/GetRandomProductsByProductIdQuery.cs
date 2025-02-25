using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetRandoms.GetRandomProductsByProductId;

public class GetRandomProductsByProductIdQuery : IRequest<GetListResponse<GetRandomProductsByProductIdQueryResponse>>, ICachableRequest
{
    public string ProductId { get; set; }
    
    public string CacheKey => $"GetRandomProductsByProductIdQuery({ProductId})";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);

    
    public class GetRandomProductsByProductIdQueryHandler : IRequestHandler<GetRandomProductsByProductIdQuery, GetListResponse<GetRandomProductsByProductIdQueryResponse>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public GetRandomProductsByProductIdQueryHandler(IProductRepository productRepository, IStorageService storageService, IMapper mapper)
        {
            _productRepository = productRepository;
            _storageService = storageService;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetRandomProductsByProductIdQueryResponse>> Handle(GetRandomProductsByProductIdQuery request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetAsync(predicate: x => x.Id == request.ProductId, include: x => x.Include(x => x.Category));
            var categoryId = product.CategoryId;

            var products = await _productRepository.GetListAsync(
                predicate: x => x.CategoryId == categoryId && x.Id != request.ProductId,
                include: x => x
                    .Include(x => x.Category)
                    .Include(x => x.Brand)
                    .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase == true))
                    .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature)
                );

            var randomProducts = products.Items
                .OrderBy(x => Guid.NewGuid())
                .Take(10)
                .ToList();

            GetListResponse<GetRandomProductsByProductIdQueryResponse> response = _mapper.Map<GetListResponse<GetRandomProductsByProductIdQueryResponse>>(randomProducts);
            
            foreach (var productDto in response.Items)
            {
                var productEntity = randomProducts.First(p => p.Id == productDto.Id);
                var showcaseImage = productEntity.ProductImageFiles?.FirstOrDefault();
                if (showcaseImage != null)
                {
                    productDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }
            }
           
            return response;
        
        }
    }
   
}