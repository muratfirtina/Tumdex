using Application.Features.Categories.Rules;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Core.CrossCuttingConcerns.Exceptions; 

namespace Application.Features.Categories.Commands.Update;
public class UpdateCategoryCommand : IRequest<UpdatedCategoryResponse>, ITransactionalRequest, ICacheRemoverRequest
{
    public string Id { get; set; }
    public string? Name { get; set; } 
    public string? Title { get; set; }
    public string? ParentCategoryId { get; set; }
    public List<string>? SubCategoryIds { get; set; }
    public List<string>? FeatureIds { get; set; }
    public List<IFormFile>? NewCategoryImage { get; set; }
    public bool RemoveExistingImage { get; set; }
    
    public string CacheKey => $"Category-{Id}";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.ProductRelated;

    public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, UpdatedCategoryResponse>
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly CategoryBusinessRules _categoryBusinessRules;
        private readonly IFeatureRepository _featureRepository;
        private readonly IStorageService _storageService;
        private readonly IImageFileRepository _imageFileRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<UpdateCategoryCommandHandler> _logger; 

        public UpdateCategoryCommandHandler(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            CategoryBusinessRules categoryBusinessRules,
            IFeatureRepository featureRepository,
            IStorageService storageService,
            IImageFileRepository imageFileRepository,
            ILogger<UpdateCategoryCommandHandler> logger)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _categoryBusinessRules = categoryBusinessRules;
            _featureRepository = featureRepository;
            _storageService = storageService;
            _imageFileRepository = imageFileRepository;
            _logger = logger; // Atandı
        }

        public async Task<UpdatedCategoryResponse> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to update category with ID: {CategoryId}", request.Id);

            // Kategoriyi ilişkili verilerle getir (Features, CategoryImageFiles)
            Category? category = await _categoryRepository.GetAsync(p => p.Id == request.Id,
                include: c => c.Include(c => c.Features)
                              .Include(c => c.CategoryImageFiles)
                              .Include(c => c.ParentCategory)
                              .Include(c => c.SubCategories),
                cancellationToken: cancellationToken);

            // İş Kuralları
            await _categoryBusinessRules.CategoryShouldExistWhenSelected(category);
             if (category == null) throw new BusinessException($"Category with ID '{request.Id}' not found."); // Daha spesifik exception

            // İsim güncelleniyorsa benzersizlik kontrolü (kendi ID'si hariç)
            if (request.Name != null && request.Name != category.Name)
            {
                await _categoryBusinessRules.CategoryNameShouldBeUniqueWhenUpdate(request.Name, request.Id, cancellationToken);
                category.Name = request.Name;
                 _logger.LogDebug("Category name updated for ID: {CategoryId}", request.Id);
            }

            // Title güncelleme
            if (request.Title != null)
            {
                category.Title = request.Title;
                 _logger.LogDebug("Category title updated for ID: {CategoryId}", request.Id);
            }

            // Parent Kategori Güncelleme
            // ParentCategoryId request'te varsa (null değilse)
            if (request.ParentCategoryId != null)
            {
                 // Kendisi parent olamaz
                 await _categoryBusinessRules.ParentCategoryShouldNotBeSelf(request.Id, request.ParentCategoryId, cancellationToken);
                 // Kendi alt kategorisi parent olamaz
                 await _categoryBusinessRules.ParentCategoryShouldNotBeChild(request.Id, request.ParentCategoryId, cancellationToken);
                 // Döngüsel bağımlılık kontrolü (parent kendi alt ağacında olamaz)
                 await _categoryBusinessRules.ParentCategoryShouldNotBeDescendant(request.Id, request.ParentCategoryId, cancellationToken);

                 // Yeni parent'ı bul
                 var parentCategory = await _categoryRepository.GetAsync(c => c.Id == request.ParentCategoryId, cancellationToken: cancellationToken);
                 if (parentCategory == null && request.ParentCategoryId != "")
                 {
                     _logger.LogError("New parent category not found with ID: {ParentCategoryId}", request.ParentCategoryId);
                     throw new BusinessException($"Parent category with ID '{request.ParentCategoryId}' not found.");
                 }

                 // Parent'ı ata (null ise üst seviye olur)
                 category.ParentCategory = parentCategory; // null atamak ilişkiyi koparır
                 category.ParentCategoryId = parentCategory?.Id; // null veya yeni ID
                 _logger.LogDebug("Parent category updated for ID: {CategoryId} to ParentId: {ParentCategoryId}", request.Id, category.ParentCategoryId ?? "NULL");
            }
             // Eğer request.ParentCategoryId null değil ama boş string ise (""), parent'ı kaldır.
             else if (request.ParentCategoryId == "") // Parent'ı kaldırma isteği
             {
                 category.ParentCategory = null;
                 category.ParentCategoryId = null;
                 _logger.LogDebug("Parent category removed for ID: {CategoryId}", request.Id);
             }
             // Eğer request.ParentCategoryId null ise, bu alan güncellenmek istenmiyor demektir, dokunma.


            // Feature Güncelleme
            if (request.FeatureIds != null) // Feature listesi geldiyse güncelle (boş liste dahil)
            {
                _logger.LogDebug("Updating features for category ID: {CategoryId}", request.Id);
                category.Features?.Clear(); // Önce mevcutları temizle (ilişki tablosu için)
                if (request.FeatureIds.Any())
                {
                     ICollection<Feature> features = new List<Feature>();
                     foreach (var featureId in request.FeatureIds)
                     {
                         var feature = await _featureRepository.GetAsync(f => f.Id == featureId, cancellationToken: cancellationToken);
                         if (feature == null)
                         {
                             _logger.LogError("Feature not found with ID: {FeatureId} while updating category {CategoryId}", featureId, request.Id);
                             throw new BusinessException($"Feature with id {featureId} not found");
                         }
                         features.Add(feature);
                     }
                     category.Features = features; // Yeni listeyi ata
                     _logger.LogDebug("{FeatureCount} features assigned to category ID: {CategoryId}", features.Count, request.Id);
                }
                else
                {
                    _logger.LogDebug("All features removed from category ID: {CategoryId}", request.Id);
                }
            }
            // Eğer request.FeatureIds null ise, özelliklere dokunma.


            // Resim Güncelleme
            // 1. Mevcut resmi silme isteği varsa
            if (request.RemoveExistingImage)
            {
                var existingImage = category.CategoryImageFiles?.FirstOrDefault();
                if (existingImage != null)
                {
                    _logger.LogInformation("Removing existing image for category ID: {CategoryId}", request.Id);
                    try
                    {
                        await _storageService.DeleteFromAllStoragesAsync("categories", existingImage.Path, existingImage.Name);
                        category.CategoryImageFiles?.Remove(existingImage); // İlişkiyi kopar
                        await _imageFileRepository.DeleteAsync(existingImage); // DB'den sil
                        _logger.LogDebug("Removed existing image {FileName} from storage and DB.", existingImage.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error removing existing image {FileName} for category ID: {CategoryId}", existingImage.Name, request.Id);
                    }
                }
                 else
                 {
                     _logger.LogWarning("Request to remove existing image for category ID {CategoryId}, but no existing image found.", request.Id);
                 }
            }

            // 2. Yeni resim(ler) yüklendiyse (ve mevcut resim silinmediyse veya zaten yoksa)
            if (request.NewCategoryImage != null && request.NewCategoryImage.Any())
            {
                // Yeni resim varsa, mevcut olanı (varsa ve silinmediyse) her zaman sil.
                var existingImage = category.CategoryImageFiles?.FirstOrDefault();
                 if (existingImage != null)
                 {
                     _logger.LogInformation("Replacing existing image with new upload for category ID: {CategoryId}", request.Id);
                     try
                     {
                         await _storageService.DeleteFromAllStoragesAsync("categories", existingImage.Path, existingImage.Name);
                         category.CategoryImageFiles?.Remove(existingImage);
                         await _imageFileRepository.DeleteAsync(existingImage);
                         _logger.LogDebug("Removed existing image {FileName} before uploading new one.", existingImage.Name);
                     }
                     catch (Exception ex)
                     {
                         _logger.LogError(ex, "Error removing existing image {FileName} before uploading new one for category ID: {CategoryId}", existingImage.Name, request.Id);
                     }
                 }

                 _logger.LogInformation("Uploading new image for category ID: {CategoryId}", request.Id);
                 var uploadedImage = await _storageService.UploadAsync("categories", category.Id, request.NewCategoryImage); // Genellikle tek resim yüklenir.
                 if(uploadedImage.Any())
                 {
                    var file = uploadedImage.First(); // İlk resmi al
                     var categoryImageFile = new CategoryImageFile(file.fileName, file.entityType, file.path, file.storageType)
                     {
                         Format = file.format
                     };
                     category.CategoryImageFiles = new List<CategoryImageFile> { categoryImageFile }; // Koleksiyona ekle/ata
                     _logger.LogDebug("New image {FileName} uploaded and associated with category ID: {CategoryId}", file.fileName, request.Id);
                 }
            }

            // Güncelleme tarihini ayarla
            category.UpdatedDate = DateTime.UtcNow;

            // Kategoriyi DB'de güncelle
            await _categoryRepository.UpdateAsync(category);
            _logger.LogInformation("Category updated successfully in database: {CategoryId}", request.Id);

            // Response oluştur
            UpdatedCategoryResponse response = _mapper.Map<UpdatedCategoryResponse>(category);
            
            return response;
        }
    }
}