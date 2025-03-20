using Application.Consts;
using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Features.Brands.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Brands.Queries.GetBrandsByIds;

public class GetBrandsByIdsQuery : IRequest<GetListResponse<GetBrandsByIdsQueryResponse>>, ICachableRequest
{
    public List<string> Ids { get; set; }
    
    // More descriptive cache key with IDs
    public string CacheKey => $"Brands-ByIds-{string.Join(",", Ids)}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
   
    public class GetBrandsByIdsQueryHandler : IRequestHandler<GetBrandsByIdsQuery, GetListResponse<GetBrandsByIdsQueryResponse>>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetBrandsByIdsQueryHandler(IBrandRepository brandRepository, IMapper mapper, IStorageService storageService)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetBrandsByIdsQueryResponse>> Handle(GetBrandsByIdsQuery request, CancellationToken cancellationToken)
        {
            List<Brand> brands = await _brandRepository.GetAllAsync(
                index:-1,
                size:-1,
                predicate: x => request.Ids.Contains(x.Id),
                include: c => c
                    .Include(c => c.BrandImageFiles)
                    .Include(fv => fv.Products),
                cancellationToken: cancellationToken
            );

            var response = _mapper.Map<GetListResponse<GetBrandsByIdsQueryResponse>>(brands);

            // Her marka için görsel dönüşümünü yap
            foreach (var brandDto in response.Items)
            {
                var brand = brands.FirstOrDefault(b => b.Id == brandDto.Id);
                if (brand?.BrandImageFiles != null)
                {
                    var brandImage = brand.BrandImageFiles.FirstOrDefault();
                    if (brandImage != null)
                    {
                        brandDto.BrandImage = brandImage.ToDto(_storageService);
                    }
                }
            }

            return response;
        }
    }
}