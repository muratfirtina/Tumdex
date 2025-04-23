using Application.Extensions.ImageFileExtensions;
using Application.Features.Brands.Consts;
using Application.Features.Brands.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Core.CrossCuttingConcerns.Exceptions;

using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Brands.Commands.Update;

public class UpdateBrandCommand : IRequest<UpdatedBrandResponse>, ITransactionalRequest, ICacheRemoverRequest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<IFormFile>? BrandImage { get; set; }
    public bool RegenerateId { get; set; } = true;
    public string CacheKey => $"Brand-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => $"{CacheGroups.Brands},{CacheGroups.Products},{CacheGroups.Categories}";
    public class UpdateBrandCommandHandler : IRequestHandler<UpdateBrandCommand, UpdatedBrandResponse>
    {
        private readonly IBrandRepository _brandRepository;
        private readonly BrandBusinessRules _brandBusinessRules;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly IImageFileRepository _imageFileRepository;
        private readonly ILogger<UpdateBrandCommandHandler> _logger;

        public UpdateBrandCommandHandler(
            IBrandRepository brandRepository,
            IMapper mapper,
            BrandBusinessRules brandBusinessRules,
            IStorageService storageService,
            IImageFileRepository imageFileRepository,
            ILogger<UpdateBrandCommandHandler> logger)
        {
            _brandRepository = brandRepository;
            _mapper = mapper;
            _brandBusinessRules = brandBusinessRules;
            _storageService = storageService;
            _imageFileRepository = imageFileRepository;
            _logger = logger;
        }

        public async Task<UpdatedBrandResponse> Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to update brand with ID: {BrandId}", request.Id);

            Brand? brand = await _brandRepository.GetAsync(
                p => p.Id == request.Id,
                include: x => x.Include(b => b.Products)
                              .Include(b => b.BrandImageFiles), 
                cancellationToken: cancellationToken);


            await _brandBusinessRules.BrandShouldExistWhenSelected(brand);
            await _brandBusinessRules.BrandNameShouldNotExistWhenInsertingOrUpdating(request.Name, request.Id);
            
            if (brand == null)
                throw new BusinessException(BrandsBusinessMessages.BrandNotExists); 
            string oldId = brand.Id;
            Brand brandToUpdate = brand;

            // ID Yeniden Oluşturma Mantığı (Eğer isim değiştiyse ve RegenerateId true ise)
            // DİKKAT: ID değiştirmek genellikle önerilmez. İlişkili tablolarda sorun yaratabilir.
            // Bu mantık yerine sadece Name ve diğer alanları güncellemek daha yaygındır.
            // Eğer ID değişimi şartsa, ilişkili tüm kayıtların (örn. Products.BrandId) güncellenmesi gerekir.
            // Bu örnekte ID değişimi mantığını DEVRE DIŞI BIRAKIYORUZ, sadece güncelleme yapıyoruz.
            /*
            if (request.RegenerateId && brand.Name != request.Name)
            {
                _logger.LogWarning("Brand ID regeneration requested for ID: {OldBrandId} due to name change. This is generally not recommended.", oldId);
                // ID değişimi çok riskli, bu kısmı dikkatlice implemente etmek gerekir.
                // Yeni ID ile yeni bir kayıt oluşturup eskisini silmek ve TÜM ilişkileri güncellemek gerekir.
                // Bu örnekte bu kompleks senaryoyu atlıyoruz.
                // throw new NotImplementedException("Brand ID regeneration logic is complex and not implemented in this example.");
            }
            */

            // Marka bilgilerini güncelle
            brandToUpdate.Name = request.Name;
            brandToUpdate.UpdatedDate = DateTime.UtcNow; // Güncelleme tarihini ayarla

            // Resim Güncelleme Mantığı
            if (request.BrandImage != null && request.BrandImage.Any())
            {
                _logger.LogInformation("New images provided for brand ID: {BrandId}. Replacing existing images.", brandToUpdate.Id);
                // 1. Mevcut resimleri depolama alanından ve DB'den sil
                if (brandToUpdate.BrandImageFiles != null && brandToUpdate.BrandImageFiles.Any())
                {
                    _logger.LogDebug("Deleting {Count} existing images for brand ID: {BrandId}", brandToUpdate.BrandImageFiles.Count, brandToUpdate.Id);
                    List<BrandImageFile> imagesToDelete = brandToUpdate.BrandImageFiles.ToList(); // Kopya al
                    foreach (var oldImage in imagesToDelete)
                    {
                        try
                        {
                            // Önce depolamadan silmeyi dene
                            await _storageService.DeleteFromAllStoragesAsync(oldImage.EntityType, oldImage.Path, oldImage.Name);
                            // Sonra DB'den sil (ilişkiyi kopararak veya doğrudan ImageFileRepository ile)
                            // brandToUpdate.BrandImageFiles.Remove(oldImage); // İlişkiyi kopar
                            await _imageFileRepository.DeleteAsync(oldImage); // Doğrudan sil (daha güvenli olabilir)
                            _logger.LogDebug("Deleted image: {FileName} from storage and DB.", oldImage.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deleting existing image file: {FileName} for brand ID: {BrandId}", oldImage.Name, brandToUpdate.Id);
                            // Silme hatası durumunda ne yapılacağına karar verilmeli (örn. devam et, hata fırlat)
                        }
                    }
                     // İlişkiyi EF Core üzerinden temizle (DeleteAsync sonrası gerekirse)
                     brandToUpdate.BrandImageFiles.Clear();
                }

                // 2. Yeni resimleri yükle ve DB'ye ekle
                 _logger.LogInformation("Uploading {Count} new images for brand ID: {BrandId}", request.BrandImage.Count, brandToUpdate.Id);
                var uploadResult = await _storageService.UploadAsync("brands", brandToUpdate.Id, request.BrandImage);
                var newImages = new List<BrandImageFile>();
                foreach (var (fileName, path, entityType, storageType, url, format) in uploadResult)
                {
                    var brandImageFile = new BrandImageFile(fileName, entityType, path, storageType) // Değişken isimleri eşleşti
                    {
                         Format = format // Formatı ekle
                    };
                    newImages.Add(brandImageFile);
                     _logger.LogDebug("Prepared new BrandImageFile entity: {FileName}", fileName);
                }
                 // Yeni resimleri koleksiyona ata (eski clear edildiği için direkt atama)
                brandToUpdate.BrandImageFiles = newImages;
            }
            else
            {
                _logger.LogInformation("No new images provided for brand ID: {BrandId}. Existing images are kept.", brandToUpdate.Id);
            }

            // Markayı veritabanında güncelle
            await _brandRepository.UpdateAsync(brandToUpdate);
            _logger.LogInformation("Brand updated successfully in the database: {BrandId}", brandToUpdate.Id);

            // Response oluştur
            var response = _mapper.Map<UpdatedBrandResponse>(brandToUpdate);
            response.OldId = (oldId != brandToUpdate.Id) ? oldId : null; // ID değiştiyse eski ID'yi ekle

            // Response'a güncel resim bilgilerini ekle (varsa)
            if (brandToUpdate.BrandImageFiles != null && brandToUpdate.BrandImageFiles.Any())
            {
                response.BrandImage = brandToUpdate.BrandImageFiles.Select(img => img.ToDto(_storageService)).FirstOrDefault();
            }

            _logger.LogInformation("Brand update process completed for ID: {BrandId}", brandToUpdate.Id);
            return response;
        }
    }
}
