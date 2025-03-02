using Application.Consts;
using Application.Extensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Queries.GetSubCategoriesByCategoryId;

public class GetSubCategoriesByCategoryIdQuery : IRequest<GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>>,ICachableRequest
{
    public string ParentCategoryId { get; set; }

    public string CacheKey => $"GetSubCategoriesByCategoryIdQuery({ParentCategoryId})";
    public bool BypassCache { get; } = true;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetSubCategoriesByCategoryIdQueryHandler : IRequestHandler<GetSubCategoriesByCategoryIdQuery, GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetSubCategoriesByCategoryIdQueryHandler(ICategoryRepository categoryRepository, IMapper mapper, IStorageService storageService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>> Handle(GetSubCategoriesByCategoryIdQuery request, CancellationToken cancellationToken)
        {
            //gelen parentCategoryId'ye göre subCategoryleri getir.
            List<Category> categories = await _categoryRepository.GetAllAsync(
                index:-1,
                size:-1,
                predicate: x => x.ParentCategoryId == request.ParentCategoryId,
                include: c => c.Include(c => c.ParentCategory)
                    .Include(c => c.CategoryImageFiles),
                cancellationToken: cancellationToken
            );
            
            GetListResponse<GetSubCategoriesByCategoryIdQueryReponse> response = _mapper.Map<GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>>(categories);
            response.Items.SetImageUrls(_storageService);
            return response;
            
        }
    }
}