using Application.Consts;
using Application.Extensions;
using Application.Features.Categories.Dtos;
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

namespace Application.Features.Categories.Queries.GetMainCategories;

public class GetMainCategoiesQuery : IRequest<GetListResponse<GetMainCategoriesResponse>>, ICachableRequest
{
    public PageRequest PageRequest { get; set; }
    public string CacheKey => $"MainCategories-Page{PageRequest.PageIndex}-Size{PageRequest.PageSize}";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);

    public class
        GetMainCategoriesQueryHandler : IRequestHandler<GetMainCategoiesQuery,
        GetListResponse<GetMainCategoriesResponse>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetMainCategoriesQueryHandler(ICategoryRepository categoryRepository, IMapper mapper,
            IStorageService storageService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetMainCategoriesResponse>> Handle(GetMainCategoiesQuery request,
            CancellationToken cancellationToken)
        {
            if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
            {
                List<Category> categories = await _categoryRepository.GetAllAsync(
                    predicate: x => x.ParentCategoryId == null,
                    include: x => x
                        .Include(x => x.CategoryImageFiles)
                        .Include(x => x.Products)
                        .Include(x => x.SubCategories)
                        .ThenInclude(sc => sc.Products), // Alt kategorilerin ürünlerini include ediyoruz
                    cancellationToken: cancellationToken);

                GetListResponse<GetMainCategoriesResponse> response =
                    _mapper.Map<GetListResponse<GetMainCategoriesResponse>>(categories);
                response.Items.SetImageUrls(_storageService);
                return response;
            }
            else
            {
                IPaginate<Category> categories = await _categoryRepository.GetListAsync(
                    index: request.PageRequest.PageIndex,
                    size: request.PageRequest.PageSize,
                    predicate: x => x.ParentCategoryId == null,
                    include: x => x
                        .Include(x => x.CategoryImageFiles)
                        .Include(x => x.Products)
                        .Include(x => x.SubCategories)
                        .ThenInclude(sc => sc.Products),
                    cancellationToken: cancellationToken);

                GetListResponse<GetMainCategoriesResponse> response =
                    _mapper.Map<GetListResponse<GetMainCategoriesResponse>>(categories);
                response.Items.SetImageUrls(_storageService);
                return response;
            }
        }
    }
}