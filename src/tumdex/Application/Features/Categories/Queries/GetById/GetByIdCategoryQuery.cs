using Application.Extensions;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.CrossCuttingConcerns.Exceptions;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Categories.Queries.GetById;
public class GetByIdCategoryQuery : IRequest<GetByIdCategoryResponse>, ICachableRequest
{
    public string Id { get; set; }

    // ICachableRequest implementation
    public string CacheKey => $"Category-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Categories;
    public TimeSpan? SlidingExpiration => TimeSpan.FromHours(1);

    // --- Handler ---
    public class GetByIdCategoryQueryHandler : IRequestHandler<GetByIdCategoryQuery, GetByIdCategoryResponse>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetByIdCategoryQueryHandler> _logger;

        public GetByIdCategoryQueryHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetByIdCategoryQueryHandler> logger)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetByIdCategoryResponse> Handle(GetByIdCategoryQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching category by ID: {CategoryId}", request.Id);

            // Kategoriyi ve ilgili verileri (response DTO'suna göre) al
            Category? category = await _categoryRepository.GetAsync(
                predicate: p => p.Id == request.Id,
                include: c => c.Include(c => c.ParentCategory)
                              .Include(c => c.CategoryImageFiles)
                              .Include(c => c.SubCategories)
                              .Include(c => c.Features)
                                .ThenInclude(f => f.FeatureValues)
                              .Include(c => c.Products),
                cancellationToken: cancellationToken);

            // Kategori bulunamazsa hata fırlat
            if (category == null)
            {
                _logger.LogWarning("Category not found with ID: {CategoryId}", request.Id);
                throw new BusinessException($"Category with ID '{request.Id}' not found.");
            }

            // AutoMapper ile DTO'ya dönüştür
            GetByIdCategoryResponse response = _mapper.Map<GetByIdCategoryResponse>(category);

            // Ek bilgileri ve resmi ayarla
            var categoryImage = category.CategoryImageFiles?.FirstOrDefault();
            if (categoryImage != null)
            {
                response.SetImageUrl(_storageService);
            }
            response.ParentCategoryName = category.ParentCategory?.Name;

            _logger.LogInformation("Successfully fetched category by ID: {CategoryId}", request.Id);
            return response;
        }
    }
}