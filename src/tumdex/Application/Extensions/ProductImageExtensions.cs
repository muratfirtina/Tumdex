using Application.Features.Brands.Dtos;
using Application.Features.Carousels.Dtos;
using Application.Features.Categories.Dtos;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Application.Storage;
using Domain;

namespace Application.Extensions;

public static class ProductImageExtensions
{
    public static void SetImageUrls<T>(this IEnumerable<T> items, IStorageService storageService) where T : class
    {
        if (items == null || storageService == null)
            return;

        try
        {
            var baseUrl = storageService.GetStorageUrl()?.TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                return;

            foreach (var item in items)
            {
                SetImageUrl(item, baseUrl);
            }
        }
        catch (Exception)
        {
            // Hata durumunda sessizce devam et
        }
    }

    public static ProductImageFileDto? SetImageUrl<T>(this T item, IStorageService storageService) where T : class
    {
        if (item == null || storageService == null)
            return null;

        try
        {
            var baseUrl = storageService.GetStorageUrl()?.TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                return null;

            SetImageUrl(item, baseUrl);
        }
        catch (Exception)
        {
            // Hata durumunda sessizce devam et
        }

        return null;
    }

    private static void SetImageUrl<T>(T item, string baseUrl) where T : class
    {
        try
        {
            if (item is IHasShowcaseImage showcaseItem && showcaseItem.ShowcaseImage != null)
            {
                SetShowcaseImageUrl(showcaseItem, baseUrl);
            }

            if (item is IHasProductImageFiles productImagesItem && productImagesItem.ProductImageFiles != null)
            {
                foreach (var imageFile in productImagesItem.ProductImageFiles)
                {
                    if (imageFile != null)
                        SetProductImageFileUrl(imageFile, baseUrl);
                }
            }

            if (item is IHasRelatedProducts relatedProductsItem && relatedProductsItem.RelatedProducts != null)
            {
                foreach (var relatedProduct in relatedProductsItem.RelatedProducts)
                {
                    if (relatedProduct != null)
                        SetShowcaseImageUrl(relatedProduct, baseUrl);
                }
            }

            if (item is IHasCategoryImage categoryItem && categoryItem.CategoryImage != null)
            {
                SetCategoryImageUrl(categoryItem.CategoryImage, baseUrl);
            }

            if (item is IHasBrandImage brandItem && brandItem.BrandImage != null)
            {
                SetBrandImageUrl(brandItem.BrandImage, baseUrl);
            }

            if (item is IHasCarouselImageFiles carouselItem && carouselItem.CarouselImageFiles != null)
            {
                foreach (var imageFile in carouselItem.CarouselImageFiles)
                {
                    if (imageFile != null)
                        SetCarouselImageUrl(imageFile, baseUrl);
                }
            }
        }
        catch (Exception)
        {
            // Hata durumunda sessizce devam et
        }
    }

    private static void SetShowcaseImageUrl(IHasShowcaseImage item, string baseUrl)
    {
        if (item.ShowcaseImage == null)
        {
            item.ShowcaseImage = new ProductImageFileDto
            {
                FileName = string.Empty,
                Path = string.Empty,
                EntityType = string.Empty,
                Storage = string.Empty
            };
        }

        SetProductImageFileUrl(item.ShowcaseImage, baseUrl);
    }

    private static void SetProductImageFileUrl(ProductImageFileDto imageFile, string baseUrl)
    {
        if (imageFile == null || string.IsNullOrEmpty(baseUrl))
            return;

        imageFile.Url = string.IsNullOrEmpty(imageFile.FileName)
            ? $"{baseUrl}/{imageFile.EntityType}"
            : $"{baseUrl}/{imageFile.EntityType}/{imageFile.Path?.TrimStart('/')}/{imageFile.FileName}";
    }

    private static void SetCategoryImageUrl(CategoryImageFileDto imageFile, string baseUrl)
    {
        if (imageFile == null || string.IsNullOrEmpty(baseUrl))
            return;

        imageFile.Url = string.IsNullOrEmpty(imageFile.FileName)
            ? $"{baseUrl}/{imageFile.EntityType}"
            : $"{baseUrl}/{imageFile.EntityType}/{imageFile.Path?.TrimStart('/')}/{imageFile.FileName}";
    }

    private static void SetBrandImageUrl(BrandImageFileDto imageFile, string baseUrl)
    {
        if (imageFile == null || string.IsNullOrEmpty(baseUrl))
            return;

        imageFile.Url = string.IsNullOrEmpty(imageFile.FileName)
            ? $"{baseUrl}/{imageFile.EntityType}"
            : $"{baseUrl}/{imageFile.EntityType}/{imageFile.Path?.TrimStart('/')}/{imageFile.FileName}";
    }

    private static void SetCarouselImageUrl(CarouselImageFileDto imageFile, string baseUrl)
    {
        if (imageFile == null || string.IsNullOrEmpty(baseUrl))
            return;

        imageFile.Url = string.IsNullOrEmpty(imageFile.FileName)
            ? $"{baseUrl}/{imageFile.EntityType}"
            : $"{baseUrl}/{imageFile.EntityType}/{imageFile.Path?.TrimStart('/')}/{imageFile.FileName}";
    }
}

public interface IHasShowcaseImage
{
    ProductImageFileDto ShowcaseImage { get; set; }
}

public interface IHasProductImageFiles
{
    ICollection<ProductImageFileDto> ProductImageFiles { get; set; }
}

public interface IHasRelatedProducts
{
    List<RelatedProductDto> RelatedProducts { get; set; }
}

public interface IHasCategoryImage
{
    CategoryImageFileDto CategoryImage { get; set; }
}

public interface IHasBrandImage
{
    BrandImageFileDto BrandImage { get; set; }
}

public interface IHasCarouselImageFiles
{
    ICollection<CarouselImageFileDto> CarouselImageFiles { get; set; }
}