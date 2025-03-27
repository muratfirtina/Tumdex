using Application.Consts;
using Application.Extensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Responses;
using MediatR;

namespace Application.Features.Categories.Queries.GetAllSubCategoriesRecursive;

public class GetAllSubCategoriesRecursiveQuery : IRequest<GetListResponse<GetAllSubCategoriesRecursiveResponse>>, ICachableRequest
{
    public string ParentCategoryId { get; set; }
    public string CacheKey => $"AllSubCategories-{ParentCategoryId}";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetAllSubCategoriesRecursiveQueryHandler : IRequestHandler<GetAllSubCategoriesRecursiveQuery, GetListResponse<GetAllSubCategoriesRecursiveResponse>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetAllSubCategoriesRecursiveQueryHandler(ICategoryRepository categoryRepository, IMapper mapper, IStorageService storageService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetAllSubCategoriesRecursiveResponse>> Handle(GetAllSubCategoriesRecursiveQuery request, CancellationToken cancellationToken)
        {
            var allSubCategories = await _categoryRepository.GetAllSubCategoriesRecursiveAsync(request.ParentCategoryId, cancellationToken);
            
            GetListResponse<GetAllSubCategoriesRecursiveResponse> response = _mapper.Map<GetListResponse<GetAllSubCategoriesRecursiveResponse>>(allSubCategories);
            response.Items.SetImageUrls(_storageService);
            return response;
        }
    }
}