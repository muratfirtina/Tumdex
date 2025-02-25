using Application.Features.Categories.Dtos;
using Application.Storage;
using Domain;

namespace Application.Extensions.ImageFileExtensions;

public static class CategoryImageExtensions
{
    public static CategoryImageFileDto ToDto(
        this CategoryImageFile imageFile, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        if (imageFile == null) return null;

        return new CategoryImageFileDto
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
        this CategoryImageFile imageFile, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return $"{storageService.GetStorageUrl(preferredStorage)}/{imageFile.EntityType}/{imageFile.Path}/{imageFile.Name}";
    }
}