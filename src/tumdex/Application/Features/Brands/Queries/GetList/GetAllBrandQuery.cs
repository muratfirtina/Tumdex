using Application.Consts;
using Application.Extensions;
using Application.Features.Brands.Dtos;
using Application.Features.ProductImageFiles.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Brands.Queries.GetList;

public class GetAllBrandQuery : IRequest<GetListResponse<GetAllBrandQueryResponse>>, ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    
    // More descriptive cache key with pagination info
    public string CacheKey => $"Brands-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetAllBrandQueryHandler : IRequestHandler<GetAllBrandQuery, GetListResponse<GetAllBrandQueryResponse>>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetAllBrandQueryHandler(IBrandRepository brandRepository, IMapper mapper, IStorageService storageService)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetAllBrandQueryResponse>> Handle(GetAllBrandQuery request, CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                List<Brand> brands = await _brandRepository.GetAllAsync(
                    include: x => x.Include(x => x.BrandImageFiles)
                                 .Include(x => x.Products),
                    cancellationToken: cancellationToken);
                GetListResponse<GetAllBrandQueryResponse> response = _mapper.Map<GetListResponse<GetAllBrandQueryResponse>>(brands);
                response.Items.SetImageUrl(_storageService);
                return response;
            }
            else
            {
                IPaginate<Brand> brands = await _brandRepository.GetListAsync(
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    include: x => x.Include(x => x.BrandImageFiles)
                                 .Include(x => x.Products),
                    orderBy: x => x.OrderBy(x => x.Name),
                    cancellationToken: cancellationToken
                );
                GetListResponse<GetAllBrandQueryResponse> response = _mapper.Map<GetListResponse<GetAllBrandQueryResponse>>(brands);
                response.Items.SetImageUrls(_storageService);
                return response;
            }
        }
    }
}