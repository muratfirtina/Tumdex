using Application.Features.Dashboard.Dtos;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Domain;
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
        private readonly IStorageService _storageService;

        public GetRecentBrandsQueryHandler(
            IBrandRepository brandRepository, 
            IMapper mapper,
            IStorageService storageService)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetRecentBrandsResponse> Handle(GetRecentBrandsQuery request, CancellationToken cancellationToken)
        {
            var recentBrands = await _brandRepository.GetAllAsync(
                orderBy: query => query.OrderByDescending(b => b.CreatedDate),
                index: 0,
                size: request.Count,
                enableTracking: false,
                cancellationToken: cancellationToken);

            // Uygun şekilde dönüştür ve URL oluştur
            var result = recentBrands.Select(b => 
            {
                var imageFile = b.BrandImageFiles.FirstOrDefault();
                
                var dto = new RecentItemDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    CreatedDate = b.CreatedDate
                };
                
                if (imageFile != null)
                {
                    dto.Image = imageFile.ToBaseDto(_storageService);
                }
                
                return dto;
            }).ToList();

            return new GetRecentBrandsResponse { Brands = result };
        }
    }
}