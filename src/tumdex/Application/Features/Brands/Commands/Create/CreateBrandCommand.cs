using Application.Consts;
using Application.Features.Brands.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Application.Features.Brands.Commands.Create;

public class CreateBrandCommand : IRequest<CreatedBrandResponse>, ITransactionalRequest,ICacheRemoverRequest
{
    public string Name { get; set; }
    public List<IFormFile>? BrandImage { get; set; }

    public string CacheKey => "";
    public bool BypassCache { get; }
    public string? CacheGroupKey => CacheGroups.GetAll;

    public class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, CreatedBrandResponse>
    {
        private readonly IMapper _mapper;
        private readonly IBrandRepository _brandRepository;
        private readonly IStorageService _storageService;
        private readonly BrandBusinessRules _brandBusinessRules;
        private readonly ILogger<CreateBrandCommandHandler> _logger;

        public CreateBrandCommandHandler(
            IMapper mapper,
            IBrandRepository brandRepository,
            IStorageService storageService,
            BrandBusinessRules brandBusinessRules,
            ILogger<CreateBrandCommandHandler> logger)
        {
            _mapper = mapper;
            _brandRepository = brandRepository;
            _storageService = storageService;
            _brandBusinessRules = brandBusinessRules;
            _logger = logger;
        }

        public async Task<CreatedBrandResponse> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. İş kuralı validasyonları (Transaction dışında)
                await _brandBusinessRules.BrandNameShouldNotExistWhenInsertingOrUpdating(request.Name);

                // 2. Image upload işlemi (Transaction dışında)
                List<(string fileName, string path, string entityType, string storageType, string url, string format)> uploadResult = new();
                if (request.BrandImage != null && request.BrandImage.Any())
                {
                    _logger.LogInformation("Uploading brand images for brand: {BrandName}", request.Name);
                    uploadResult = await _storageService.UploadAsync("brands", Guid.NewGuid().ToString(), request.BrandImage);
                }

                // 3. Brand oluşturma (Transaction içinde)
                var brand = _mapper.Map<Brand>(request);
                await _brandRepository.AddAsync(brand);

                // 4. Image bilgilerini kaydetme (Transaction içinde)
                if (uploadResult.Any())
                {
                    var (fileName, path, _, storageType, _, format) = uploadResult.First();
                    var brandImageFile = new BrandImageFile(fileName, "brands", path, storageType)
                    {
                        Format = format
                    };
                    brand.BrandImageFiles = new List<BrandImageFile> { brandImageFile };
                    await _brandRepository.UpdateAsync(brand);
                }

                // 5. Response mapping
                var response = _mapper.Map<CreatedBrandResponse>(brand);
                _logger.LogInformation("Brand created successfully: {BrandId}", brand.Id);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating brand: {BrandName}", request.Name);
                throw;
            }
        }
    }
    
}