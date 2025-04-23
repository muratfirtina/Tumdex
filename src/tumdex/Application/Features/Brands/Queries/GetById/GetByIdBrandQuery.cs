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

namespace Application.Features.Brands.Queries.GetById;
public class GetByIdBrandQuery : IRequest<GetByIdBrandResponse>, ICachableRequest
{
    public string Id { get; set; }
    public string CacheKey => $"Brand-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Brands;
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(60);
    public class GetByIdBrandQueryHandler : IRequestHandler<GetByIdBrandQuery, GetByIdBrandResponse>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly ILogger<GetByIdBrandQueryHandler> _logger;

        public GetByIdBrandQueryHandler(
            IBrandRepository brandRepository,
            IMapper mapper,
            IStorageService storageService,
            ILogger<GetByIdBrandQueryHandler> logger)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<GetByIdBrandResponse> Handle(GetByIdBrandQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching brand by ID: {BrandId}", request.Id);

            // Include ifadesi response DTO'suna göre ayarlanmalı
            Brand? brand = await _brandRepository.GetAsync(
                predicate: p => p.Id == request.Id,
                include: query => query
                    .Include(b => b.BrandImageFiles) // Resimler
                    .Include(b => b.Products),      // Ürün sayısı için
                cancellationToken: cancellationToken);

            // Marka bulunamazsa hata fırlat
            if (brand == null)
            {
                _logger.LogWarning("Brand not found with ID: {BrandId}", request.Id);
                 throw new BusinessException("Brand not found");
            }

            // AutoMapper ile DTO'ya dönüştür
            GetByIdBrandResponse response = _mapper.Map<GetByIdBrandResponse>(brand);

            // Ek bilgileri ve resim URL'sini ayarla
            var brandImage = brand.BrandImageFiles?.FirstOrDefault();
            if (brandImage != null)
            {
                response.BrandImage = brandImage.ToDto(_storageService);
            }
            response.ProductCount = brand.Products?.Count ?? 0;

            _logger.LogInformation("Successfully fetched brand by ID: {BrandId}", request.Id);
            return response;
        }
    }
}