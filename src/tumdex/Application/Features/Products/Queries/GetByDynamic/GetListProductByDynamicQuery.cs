using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetByDynamic;

public class GetListProductByDynamicQuery : IRequest<GetListResponse<GetListProductByDynamicDto>>,ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public DynamicQuery DynamicQuery { get; set; }
    public string CacheKey => $"GetListProductByDynamicQuery({PageRequest.PageIndex},{PageRequest.PageSize})";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
    public class GetListByDynamicProductQueryHandler : IRequestHandler<GetListProductByDynamicQuery, GetListResponse<GetListProductByDynamicDto>>
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly ProductBusinessRules _productBusinessRules;
        private readonly IStorageService _storageService;

        public GetListByDynamicProductQueryHandler(IProductRepository productRepository, IMapper mapper, ProductBusinessRules productBusinessRules, IStorageService storageService)
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _productBusinessRules = productBusinessRules;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetListProductByDynamicDto>> Handle(GetListProductByDynamicQuery request, CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                var allProducts = await _productRepository.GetAllByDynamicAsync(
                    request.DynamicQuery,
                    include: p => p.Include(e => e.Category)
                        .Include(e => e.Brand)
                        .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature)
                        .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase == true)),
                    cancellationToken: cancellationToken);

                var productsDtos = _mapper.Map<GetListResponse<GetListProductByDynamicDto>>(allProducts);
                foreach (var productsDto in productsDtos.Items)
                {
                    var product = allProducts.First(p => p.Id == productsDto.Id);
                    var showcaseImage = product.ProductImageFiles?.FirstOrDefault(pif => pif.Showcase);
                    if (showcaseImage != null)
                    {
                        productsDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                    }
                }

                return productsDtos;
            }
            else
            {
                IPaginate<Product> products = await _productRepository.GetListByDynamicAsync(
                    request.DynamicQuery,
                    include: p => p.Include(e => e.Category)
                        .Include(e => e.Brand)
                        .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature)
                        .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase == true)),
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    cancellationToken: cancellationToken);
                
                var productsDtos = _mapper.Map<GetListResponse<GetListProductByDynamicDto>>(products);
                
                if (productsDtos?.Items != null)
                {
                    productsDtos.Items.SetImageUrls(_storageService);
                }
                return productsDtos;

            }
        }
        
    }
}