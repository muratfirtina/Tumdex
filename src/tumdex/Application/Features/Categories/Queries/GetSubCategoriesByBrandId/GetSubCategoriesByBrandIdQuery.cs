using Application.Consts;
using Application.Extensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Requests;
using Core.Application.Responses;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Queries.GetSubCategoriesByBrandId;

public class GetSubCategoriesByBrandIdQuery : IRequest<GetListResponse<GetSubCategoriesByBrandIdQueryReponse>>,ICachableRequest
{
    public string BrandId { get; set; }

    public string CacheKey => $"GetSubCategoriesByBrandIdQuery({BrandId})";
    public bool BypassCache { get; } = true;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);

    
    public class GetSubCategoriesByBrandIdQueryHandler : IRequestHandler<GetSubCategoriesByBrandIdQuery, GetListResponse<GetSubCategoriesByBrandIdQueryReponse>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetSubCategoriesByBrandIdQueryHandler(ICategoryRepository categoryRepository, IMapper mapper, IStorageService storageService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetSubCategoriesByBrandIdQueryReponse>> Handle(GetSubCategoriesByBrandIdQuery request, CancellationToken cancellationToken)
        {
            //gelen brandId'ye göre subCategoryleri getir.
            List<Category> categories = await _categoryRepository.GetAllAsync(
                index:-1,
                size:-1,
                predicate: x => x.Products.Any(p => p.BrandId == request.BrandId) && x.ParentCategoryId != null,
                include: c => c.Include(c => c.CategoryImageFiles),
                cancellationToken: cancellationToken
            );
            
            GetListResponse<GetSubCategoriesByBrandIdQueryReponse> response = _mapper.Map<GetListResponse<GetSubCategoriesByBrandIdQueryReponse>>(categories);
            response.Items.SetImageUrls(_storageService);
            return response;
            
        }
    }
}