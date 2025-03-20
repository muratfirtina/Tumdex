using Application.Consts;
using Application.Extensions.ImageFileExtensions;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Brands.Queries.GetById;

public class GetByIdBrandQuery : IRequest<GetByIdBrandResponse>, ICachableRequest
{
    public string Id { get; set; }
    
    // Specific cache key with ID
    public string CacheKey => $"Brand-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.GetAll;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(30);
    
    public class GetByIdBrandQueryHandler : IRequestHandler<GetByIdBrandQuery, GetByIdBrandResponse>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;

        public GetByIdBrandQueryHandler(IBrandRepository brandRepository, IMapper mapper, IStorageService storageService)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
        }

        public async Task<GetByIdBrandResponse> Handle(GetByIdBrandQuery request, CancellationToken cancellationToken)
        {
            Brand? brand = await _brandRepository.GetAsync(
                predicate: p => p.Id == request.Id,
                include: x => x.Include(x => x.BrandImageFiles),
                cancellationToken: cancellationToken);

            if (brand == null)
            {
                throw new BusinessException("Brand not found");
            }

            GetByIdBrandResponse response = _mapper.Map<GetByIdBrandResponse>(brand);
            
            if (brand.BrandImageFiles != null)
            {
                var brandImage = brand.BrandImageFiles.FirstOrDefault();
                if (brandImage != null)
                {
                    response.BrandImage = brandImage.ToDto(_storageService);
                }
            }
            
            return response;
        }
    }
}