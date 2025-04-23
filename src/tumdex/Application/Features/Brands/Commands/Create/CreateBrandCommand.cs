using Application.Features.Brands.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Application.Features.Brands.Commands.Create;

public class CreateBrandCommand : IRequest<CreatedBrandResponse>, ITransactionalRequest, ICacheRemoverRequest
{
    public string Name { get; set; }
    public List<IFormFile>? BrandImage { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.Brands;
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
             ILogger<CreateBrandCommandHandler> logger) // Logger eklendi
         {
             _mapper = mapper;
             _brandRepository = brandRepository;
             _storageService = storageService;
             _brandBusinessRules = brandBusinessRules;
             _logger = logger; // Logger atanıyor
         }

        public async Task<CreatedBrandResponse> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
        {
             _logger.LogInformation("Attempting to create brand with Name: {BrandName}", request.Name); // Loglama
             await _brandBusinessRules.BrandNameShouldNotExistWhenInsertingOrUpdating(request.Name);

             var brand = _mapper.Map<Brand>(request);

             // Resim yükleme (transaction dışında olabilir veya içinde, stratejiye bağlı)
             List<(string fileName, string path, string entityType, string storageType, string url, string format)> uploadResult = new();
             if (request.BrandImage != null && request.BrandImage.Any())
             {
                 _logger.LogInformation("Uploading brand image for brand: {BrandName}", request.Name);
                 // Resim yüklerken entity ID'si yerine geçici bir ID veya dosya adı kullanmak daha iyi olabilir.
                 // Entity kaydedildikten sonra path güncellenebilir. Şimdilik ID kullanıyoruz.
                 uploadResult = await _storageService.UploadAsync("brands", brand.Id, request.BrandImage); // entityType ve id eklendi
             }

             // Brand ekleme
             await _brandRepository.AddAsync(brand);
             _logger.LogInformation("Brand entity added to context with ID: {BrandId}", brand.Id);

             // Resim bilgilerini kaydetme (Brand eklendikten sonra)
             if (uploadResult.Any())
             {
                var imageFiles = new List<BrandImageFile>();
                foreach (var file in uploadResult)
                {
                    var brandImageFile = new BrandImageFile(file.fileName, "brands", file.path, file.storageType) // storageType eklendi
                    {
                        
                        Format = file.format // format eklendi
                    };
                    imageFiles.Add(brandImageFile);
                }
                brand.BrandImageFiles = imageFiles;
                // EfRepositoryBase AddAsync sonrası SaveChanges yapıyorsa UpdateAsync gerekmeyebilir.
                // Ancak SaveChanges transaction içindeyse ve resim bilgisi sonra ekleniyorsa Update gerekir.
                // TransactionalRequest olduğu için SaveChanges en sonda yapılacak, bu yüzden Update gerekli.
                await _brandRepository.UpdateAsync(brand);
                _logger.LogInformation("BrandImageFile entities associated with BrandId: {BrandId}", brand.Id);
             }

             var response = _mapper.Map<CreatedBrandResponse>(brand);
             _logger.LogInformation("Brand created successfully: {BrandId}, Name: {BrandName}", brand.Id, brand.Name);

             return response;
        }
    }
}