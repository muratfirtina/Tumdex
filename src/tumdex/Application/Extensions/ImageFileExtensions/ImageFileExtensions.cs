using Application.Features.Brands.Dtos;
using Application.Features.Carousels.Dtos;
using Application.Features.Categories.Dtos;
using Application.Features.ProductImageFiles.Dtos;
using Application.Storage;
using Domain;

namespace Application.Extensions.ImageFileExtensions;

public static class ImageFileExtensions
{
    public static ProductImageFileDto ToDto(
        this ProductImageFile imageFile, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return new ProductImageFileDto
        {
            Id = imageFile.Id,
            Path = imageFile.Path,
            FileName = imageFile.Name,
            Showcase = imageFile.Showcase,
            Storage = imageFile.Storage,
            EntityType = imageFile.EntityType,
            Alt = imageFile.Alt,
            Url = $"{storageService.GetStorageUrl(preferredStorage)}/{imageFile.EntityType}/{imageFile.Path}/{imageFile.Name}"
        };
    }

    public static string SetImageUrl(
        this ProductImageFile imageFile, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return $"{storageService.GetStorageUrl(preferredStorage)}/{imageFile.EntityType}/{imageFile.Path}/{imageFile.Name}";
    }
}

public static class ImageCollectionExtensions
{
    public static List<ProductImageFileDto> ToDtos(
        this IEnumerable<ProductImageFile> imageFiles, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return imageFiles?.Select(img => img.ToDto(storageService, preferredStorage))
                         .Where(dto => dto != null)
                         .ToList() ?? new List<ProductImageFileDto>();
    }

    public static List<CategoryImageFileDto> ToDtos(
        this IEnumerable<CategoryImageFile> imageFiles, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return imageFiles?.Select(img => img.ToDto(storageService, preferredStorage))
                         .Where(dto => dto != null)
                         .ToList() ?? new List<CategoryImageFileDto>();
    }

    public static List<BrandImageFileDto> ToDtos(
        this IEnumerable<BrandImageFile> imageFiles, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return imageFiles?.Select(img => img.ToDto(storageService, preferredStorage))
                         .Where(dto => dto != null)
                         .ToList() ?? new List<BrandImageFileDto>();
    }

    public static List<CarouselImageFileDto> ToDtos(
        this IEnumerable<CarouselImageFile> imageFiles, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return imageFiles?.Select(img => img.ToDto(storageService, preferredStorage))
                         .Where(dto => dto != null)
                         .ToList() ?? new List<CarouselImageFileDto>();
    }
    
    
}