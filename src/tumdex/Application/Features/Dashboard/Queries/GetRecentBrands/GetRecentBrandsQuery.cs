using Application.Features.Dashboard.Dtos;
using Application.Repositories;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries.GetRecentBrands;

public class GetRecentBrandsQuery : IRequest<GetRecentBrandsResponse>
{
    public int Count { get; set; } = 5;
    
    public class GetRecentBrandsQueryHandler : IRequestHandler<GetRecentBrandsQuery, GetRecentBrandsResponse>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;

        public GetRecentBrandsQueryHandler(IBrandRepository brandRepository, IMapper mapper)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
        }

        public async Task<GetRecentBrandsResponse> Handle(GetRecentBrandsQuery request, CancellationToken cancellationToken)
        {
            // Markaları son eklenme tarihine göre sırala
            var include = (IQueryable<Brand> query) => query.Include(b => b.BrandImageFiles);

            var recentBrands = await _brandRepository.GetAllAsync(
                orderBy: query => query.OrderByDescending(b => b.CreatedDate),
                include: include,
                index: 0,
                size: request.Count,
                enableTracking: false,
                cancellationToken: cancellationToken);

            // AutoMapper ile DTO'lara dönüştür
            var result = recentBrands.Select(b => new RecentItemDto
            {
                Id = b.Id,
                Name = b.Name,
                CreatedDate = b.CreatedDate,
                Image = b.BrandImageFiles.FirstOrDefault()?.Path
            }).ToList();

            return new GetRecentBrandsResponse { Brands = result };
        }
    }
}