using Application.Features.Dashboard.Dtos;
using Application.Repositories;
using AutoMapper;
using Domain.Entities;
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

        public GetRecentCategoriesQueryHandler(ICategoryRepository categoryRepository, IMapper mapper)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
        }

        public async Task<GetRecentCategoriesResponse> Handle(GetRecentCategoriesQuery request, CancellationToken cancellationToken)
        {
            // Kategorileri son eklenme tarihine göre sırala
            var include = (IQueryable<Category> query) => query.Include(c => c.CategoryImageFiles);

            var recentCategories = await _categoryRepository.GetAllAsync(
                orderBy: query => query.OrderByDescending(c => c.CreatedDate),
                include: include,
                index: 0,
                size: request.Count,
                enableTracking: false,
                cancellationToken: cancellationToken);

            // AutoMapper ile DTO'lara dönüştür
            var result = recentCategories.Select(c => new RecentItemDto
            {
                Id = c.Id,
                Name = c.Name,
                CreatedDate = c.CreatedDate,
                Image = c.CategoryImageFiles.FirstOrDefault()?.Path
            }).ToList();

            return new GetRecentCategoriesResponse { Categories = result };
        }
    }
}