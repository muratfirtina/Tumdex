using Application.Features.Brands.Dtos;
using Application.Storage;
using Domain;

namespace Application.Extensions.ImageFileExtensions;

public static class BrandImageExtensions
{
    public static BrandImageFileDto ToDto(
        this BrandImageFile imageFile, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        if (imageFile == null) return null;

        return new BrandImageFileDto
        {
            Id = imageFile.Id,
            Path = imageFile.Path,
            FileName = imageFile.Name,
            Storage = imageFile.Storage,
            EntityType = imageFile.EntityType,
            Alt = imageFile.Alt,
            Url = $"{storageService.GetStorageUrl(preferredStorage)}/{imageFile.EntityType}/{imageFile.Path}/{imageFile.Name}"
        };
    }

    public static string SetImageUrl(
        this BrandImageFile imageFile, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return $"{storageService.GetStorageUrl(preferredStorage)}/{imageFile.EntityType}/{imageFile.Path}/{imageFile.Name}";
    }
}