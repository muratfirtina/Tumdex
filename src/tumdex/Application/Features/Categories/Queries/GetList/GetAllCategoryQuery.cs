using Application.Features.Categories.Dtos;
using Application.Features.Categories.Queries.GetList;
using Application.Repositories;
using AutoMapper;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Consts;
using Application.Extensions;
using Application.Storage;
using Core.Application.Pipelines.Caching;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Queries.GetList
{
    public class GetAllCategoryQuery : IRequest<GetListResponse<GetAllCategoryQueryResponse>>,ICachableRequest
    {
        public PageRequest PageRequest { get; set; }

        public string CacheKey => $"GetAllCategoryQuery({PageRequest.PageIndex},{PageRequest.PageSize})";
        public bool BypassCache { get; }
        public string? CacheGroupKey => CacheGroups.GetAll;
        public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
        
        public class GetAllCategoryQueryHandler : IRequestHandler<GetAllCategoryQuery, GetListResponse<GetAllCategoryQueryResponse>>
        {
            private readonly ICategoryRepository _categoryRepository;
            private readonly IMapper _mapper;
            private readonly IStorageService _storageService;

            public GetAllCategoryQueryHandler(ICategoryRepository categoryRepository, IMapper mapper, IStorageService storageService)
            {
                _categoryRepository = categoryRepository;
                _mapper = mapper;
                _storageService = storageService;
            }

            public async Task<GetListResponse<GetAllCategoryQueryResponse>> Handle(GetAllCategoryQuery request, CancellationToken cancellationToken)
            {
                if (request.PageRequest.PageIndex == -1 && request.PageRequest.PageSize == -1)
                {
                    List<Category> categories = await _categoryRepository.GetAllAsync(
                        include:x => x.Include(x => x.CategoryImageFiles),
                        cancellationToken: cancellationToken);
                    
                    GetListResponse<GetAllCategoryQueryResponse> response = _mapper.Map<GetListResponse<GetAllCategoryQueryResponse>>(categories);
                    response.Items.SetImageUrls(_storageService);
                    return response;
                }
                else
                {
                    IPaginate<Category> categories = await _categoryRepository.GetListAsync(
                        index: request.PageRequest.PageIndex,
                        size: request.PageRequest.PageSize,
                        include: x => x.Include(x => x.CategoryImageFiles),
                        cancellationToken: cancellationToken
                    );
                    
                    GetListResponse<GetAllCategoryQueryResponse> response = _mapper.Map<GetListResponse<GetAllCategoryQueryResponse>>(categories);
                    response.Items.SetImageUrls(_storageService);
                    return response;
                }
            }
            
            
        }
    }
}