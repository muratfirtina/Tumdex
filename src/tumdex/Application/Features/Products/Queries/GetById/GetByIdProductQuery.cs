using System.Security.Claims;
using Application.Abstraction.Services;
using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using AutoMapper.Internal;
using Core.Application.Pipelines.Caching;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetById;

public class GetByIdProductQuery : IRequest<GetByIdProductResponse>,ICachableRequest
{
    public string Id { get; set; }
    public string CacheKey => $"GetByIdProductQuery({Id})";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);

    public class GetByIdProductQueryHandler : IRequestHandler<GetByIdProductQuery, GetByIdProductResponse>
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetByIdProductQueryHandler(IProductRepository productRepository, IMapper mapper,
            IStorageService storageService)
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetByIdProductResponse> Handle(GetByIdProductQuery request,
            CancellationToken cancellationToken)
        {
            
            Product product = await _productRepository.GetAsync(
                predicate: p => p.Id == request.Id,
                cancellationToken: cancellationToken,
                include: x => x.Include(x => x.Category)
                    .Include(x => x.Brand)
                    .Include(x => x.ProductLikes)
                    .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature)
                    .Include(x => x.ProductImageFiles));
         

            var relatedProducts = await _productRepository.GetListAsync(
                predicate: p => p.VaryantGroupID == product.VaryantGroupID && p.Id != product.Id,
                include: x => x.Include(x => x.Category).Include(x => x.Brand).Include(x => x.ProductLikes)
                    .Include(x => x.ProductFeatureValues).ThenInclude(x => x.FeatureValue).ThenInclude(x => x.Feature).OrderBy(x => x.CreatedDate)
                    .Include(x => x.ProductImageFiles.Where(pif => pif.Showcase == true)),
                cancellationToken: cancellationToken);
            
            
            GetByIdProductResponse response = _mapper.Map<GetByIdProductResponse>(product);
            
            response.RelatedProducts = relatedProducts.Items.Select(rp => {
                var relatedDto = _mapper.Map<RelatedProductDto>(rp);
                var showcaseImage = rp.ProductImageFiles.FirstOrDefault();
                if (showcaseImage != null)
                {
                    relatedDto.ShowcaseImage = showcaseImage.ToDto(_storageService);
                }
                return relatedDto;
            }).ToList();

            // Ana ürünün görsellerini ayarla
            if (product.ProductImageFiles != null)
            {
                response.ProductImageFiles = product.ProductImageFiles
                    .Select(pif => pif.ToDto(_storageService))
                    .ToList();
            }
            
            // Mevcut özellikleri hesapla
            response.AvailableFeatures = new Dictionary<string, List<string>>();
            foreach (var relatedProduct in response.RelatedProducts)
            {
                foreach (var feature in relatedProduct.ProductFeatureValues)
                {
                    if (!response.AvailableFeatures.ContainsKey(feature.FeatureName))
                    {
                        response.AvailableFeatures[feature.FeatureName] = new List<string>();
                    }

                    if (!response.AvailableFeatures[feature.FeatureName].Contains(feature.FeatureValueName))
                    {
                        response.AvailableFeatures[feature.FeatureName].Add(feature.FeatureValueName);
                    }
                }
            }

            return response;
        }
    }
}