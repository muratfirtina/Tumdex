using Application.Features.Carousels.Dtos;
using Application.Storage;
using Domain;

namespace Application.Extensions.ImageFileExtensions;

public static class CarouselImageExtensions
{
    public static CarouselImageFileDto ToDto(
        this CarouselImageFile imageFile, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        if (imageFile == null) return null;

        return new CarouselImageFileDto
        {
            Id = imageFile.Id,
            Path = imageFile.Path,
            FileName = imageFile.Name,
            Storage = imageFile.Storage,
            EntityType = imageFile.EntityType,
            Url = $"{storageService.GetStorageUrl(preferredStorage)}/{imageFile.EntityType}/{imageFile.Path}/{imageFile.Name}"
        };
    }

    public static string SetImageUrl(
        this CarouselImageFile imageFile, 
        IStorageService storageService,
        string? preferredStorage = null)
    {
        return $"{storageService.GetStorageUrl(preferredStorage)}/{imageFile.EntityType}/{imageFile.Path}/{imageFile.Name}";
    }
}