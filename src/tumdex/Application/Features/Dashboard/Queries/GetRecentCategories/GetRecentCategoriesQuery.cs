using Application.Features.Dashboard.Dtos;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries.GetRecentCategories;

public class GetRecentCategoriesQuery : IRequest<GetRecentCategoriesResponse>
{
    public int Count { get; set; } = 5;
    
    public class GetRecentCategoriesQueryHandler : IRequestHandler<GetRecentCategoriesQuery, GetRecentCategoriesResponse>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetRecentCategoriesQueryHandler(
            ICategoryRepository categoryRepository, 
            IMapper mapper,
            IStorageService storageService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetRecentCategoriesResponse> Handle(GetRecentCategoriesQuery request, CancellationToken cancellationToken)
        {
            var recentCategories = await _categoryRepository.GetAllAsync(
                orderBy: query => query.OrderByDescending(c => c.CreatedDate),
                index: 0,
                size: request.Count,
                enableTracking: false,
                cancellationToken: cancellationToken);

            // Uygun şekilde dönüştür ve URL oluştur
            var result = recentCategories.Select(c => 
            {
                var imageFile = c.CategoryImageFiles.FirstOrDefault();
                
                var dto = new RecentItemDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    CreatedDate = c.CreatedDate
                };
                
                if (imageFile != null)
                {
                    dto.Image = imageFile.ToBaseDto(_storageService);
                }
                
                return dto;
            }).ToList();

            return new GetRecentCategoriesResponse { Categories = result };
        }
    }
}