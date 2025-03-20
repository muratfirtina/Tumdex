using Application.Consts;
using Application.Extensions;
using Application.Features.Categories.Dtos;
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
using System.Text.Json;

namespace Application.Features.Categories.Queries.GetCategoriesByIds;

public class GetCategoriesByIdsQuery : IRequest<GetListResponse<GetCategoriesByIdsQueryResponse>>, ICachableRequest
{
    public List<string> Ids { get; set; }
    public string CacheKey => $"Categories-ByIds-{string.Join(",",Ids)}";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetCategoriesByIdsQueryHandler : IRequestHandler<GetCategoriesByIdsQuery, GetListResponse<GetCategoriesByIdsQueryResponse>>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetCategoriesByIdsQueryHandler(ICategoryRepository categoryRepository, IMapper mapper, IStorageService storageService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetListResponse<GetCategoriesByIdsQueryResponse>> Handle(GetCategoriesByIdsQuery request, CancellationToken cancellationToken)
        {
            List<Category> categories = await _categoryRepository.GetAllAsync(
                index:-1,
                size:-1,
                predicate: x => request.Ids.Contains(x.Id),
                include: c => c.Include(c => c.ParentCategory)
                    .Include(c => c.CategoryImageFiles)
                    .Include(c => c.SubCategories)
                    .Include(c => c.Features)
                    .ThenInclude(f => f.FeatureValues)
                    .Include(fv => fv.Products),
                cancellationToken: cancellationToken
            );

            GetListResponse<GetCategoriesByIdsQueryResponse> response = _mapper.Map<GetListResponse<GetCategoriesByIdsQueryResponse>>(categories);
            response.Items.SetImageUrls(_storageService);
            return response;
        }
    }
}