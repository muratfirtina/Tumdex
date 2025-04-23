using Application.Extensions.ImageFileExtensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos.FilterDto;
using Application.Repositories;
using Application.Storage;
using Core.Application.Requests;
using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Domain.Enum;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;
using Persistence.Extensions;

namespace Persistence.Repositories;

public class ProductRepository : EfRepositoryBase<Product, string, TumdexDbContext>, IProductRepository
{
    private readonly IStorageService _storageService;

    public ProductRepository(
        TumdexDbContext context,
        IStorageService storageService) : base(context)
    {
        _storageService = storageService;
    }

    public async Task<List<ProductImageFileDto>> GetFilesByProductId(
        string productId,
        string? preferredStorage = null)
    {
        var imageFiles = await Context.Products
            .Where(p => p.Id == productId)
            .SelectMany(p => p.ProductImageFiles)
            .OrderByDescending(e => e.CreatedDate)
            .ToListAsync();

        var imageDtos = imageFiles.Select(pif => new ProductImageFileDto
        {
            Id = pif.Id,
            Path = pif.Path,
            FileName = pif.Name,
            Showcase = pif.Showcase,
            Storage = pif.Storage,
            EntityType = pif.EntityType,
            Alt = pif.Alt,
            Url = pif.SetImageUrl(_storageService, preferredStorage)
        }).ToList();

        return imageDtos;
    }

    public Task<List<ProductImageFileDto>> GetFilesByProductId(string productId)
    {
        throw new NotImplementedException();
    }

    public async Task ChangeShowcase(string productId, string imageFileId, bool showcase)
    {
        var productImageFiles = await Context.ProductImageFiles
            .Where(pif => pif.Id == productId)
            .ToListAsync();

        foreach (var pif in productImageFiles)
        {
            pif.Showcase = pif.Id == imageFileId && showcase;
        }

        await Context.SaveChangesAsync();
    }

    public async Task<ProductImageFile?> GetProductImage(string productId)
    {
        return await Context.Products
            .Where(p => p.Id == productId)
            .SelectMany(p => p.ProductImageFiles)
            .FirstOrDefaultAsync();
    }

    public async Task<(IPaginate<Product>, List<Category>, List<Brand>)> SearchProductsAsync(
        string? searchTerm,
        int pageIndex,
        int pageSize)
    {
        // Önce tüm sonuçları al
        var (products, categories, brands) = await Context.Products
            .AsQueryable()
            .SearchAllAsync(
                Context.Categories.AsQueryable(),
                Context.Brands.AsQueryable(),
                searchTerm);

        // Ürünleri paginate et
        var paginatedProducts = products
            .AsQueryable()
            .WithFullDetails()
            .WithShowcaseImage()
            .ToPaginate(pageIndex, pageSize);

        return (paginatedProducts, categories, brands);
    }

    public async Task<IPaginate<Product>> FilterProductsAsync(
        string? searchTerm,
        Dictionary<string, List<string>>? filters,
        PageRequest? pageRequest, // Nullable olarak değiştirildi
        string sortOrder)
    {
        var query = Context.Products
            .AsQueryable()
            .WithFullDetails()
            .WithShowcaseImage()
            .SearchByTerm(searchTerm);

        // Kategori filtresi uygulanırken alt kategorileri de dahil et
        if (filters != null && filters.ContainsKey("Category") && filters["Category"].Any())
        {
            var categoryIds = new List<string>(filters["Category"]);
            var allCategoryIds = new List<string>(categoryIds);
    
            // Her seçili kategori için alt kategorileri ekle
            foreach (var categoryId in categoryIds)
            {
                var subCategoryIds = await GetAllSubCategoryIds(categoryId);
                allCategoryIds.AddRange(subCategoryIds);
            }
    
            // Benzersiz kategori ID'lerini kullan
            filters["Category"] = allCategoryIds.Distinct().ToList();
        }

        query = query.ApplyFilters(filters).ApplySort(sortOrder);

        // PageRequest null ise, tüm sonuçları getir (sayfalama olmadan)
        if (pageRequest == null)
        {
            // ToPaginateAsync metodu yerine ToListAsync kullanarak veriyi çek
            var allItems = await query.ToListAsync();
        
            // Manuel olarak IPaginate tipine dönüştür
            return new Paginate<Product>
            {
                Items = allItems,
                Index = 0,
                Size = allItems.Count,
                Count = allItems.Count,
                Pages = 1
            };
        }
    
        // Normal sayfalama işlemi
        return await query.ToPaginateAsync(pageRequest.PageIndex, pageRequest.PageSize);
    }

// Bir kategorinin tüm alt kategorilerini getiren yardımcı metod:
    private async Task<List<string>> GetAllSubCategoryIds(string categoryId)
    {
        var result = new List<string>();
    
        // İlk seviye alt kategorileri al
        var subCategories = await Context.Categories
            .Where(c => c.ParentCategoryId == categoryId)
            .Select(c => c.Id)
            .ToListAsync();
    
        result.AddRange(subCategories);
    
        // Her bir alt kategori için rekursif olarak onun alt kategorilerini al
        foreach (var subCategoryId in subCategories)
        {
            var nestedSubCategories = await GetAllSubCategoryIds(subCategoryId);
            result.AddRange(nestedSubCategories);
        }
    
        return result;
    }

    public async Task<List<FilterGroupDto>> GetAvailableFilters(string? searchTerm = null, string[]? categoryIds = null, string[]? brandIds = null)
{
    var filterDefinitions = new List<FilterGroupDto>();
    
    // Başlangıç sorgusu oluştur
    var query = Context.Products.AsQueryable();
    
    // 1. Arama terimi varsa uygula
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        // Kategori ID kontrolü
        if (await Context.Categories.AnyAsync(c => c.Id == searchTerm))
        {
            query = query.Where(p => p.CategoryId == searchTerm);
        }
        // Brand ID kontrolü
        else if (await Context.Brands.AnyAsync(b => b.Id == searchTerm))
        {
            query = query.Where(p => p.BrandId == searchTerm);
        }
        else
        {
            query = query.SearchByTerm(searchTerm);
        }
    }
    
    // 2. Kategori ID'leri varsa, bunları filtreye ekle
    if (categoryIds != null && categoryIds.Length > 0)
    {
        query = query.Where(p => categoryIds.Contains(p.CategoryId));
    }
    
    // 3. Marka ID'leri varsa, bunları filtreye ekle
    if (brandIds != null && brandIds.Length > 0)
    {
        query = query.Where(p => brandIds.Contains(p.BrandId));
    }
    
    // 1. Kategori Filtresi
    var categories = await Context.Categories
        .Where(c => query.Any(p => p.CategoryId == c.Id))
        .Select(c => new FilterOptionDto
        {
            Value = c.Id,
            DisplayValue = c.Name,
            ParentId = c.ParentCategoryId
        })
        .ToListAsync();

    if (categories.Any())
    {
        filterDefinitions.Add(new FilterGroupDto
        {
            Key = "Category",
            Name = "Category",
            DisplayName = "Category",
            Type = FilterType.Checkbox,
            Options = categories
        });
    }

    // 2. Marka Filtresi
    var brands = await Context.Brands
        .Where(b => query.Any(p => p.BrandId == b.Id))
        .Select(b => new FilterOptionDto
        {
            Value = b.Id,
            DisplayValue = b.Name
        })
        .ToListAsync();

    if (brands.Any())
    {
        filterDefinitions.Add(new FilterGroupDto
        {
            Key = "Brand",
            Name = "Brand",
            DisplayName = "Brand",
            Type = FilterType.Checkbox,
            Options = brands
        });
    }

    // 3. Özellik Filtreleri
    var features = await Context.Features
        .Where(f => query.Any(p => p.ProductFeatureValues
            .Any(pfv => pfv.FeatureValue.FeatureId == f.Id)))
        .Select(f => new
        {
            FeatureName = f.Name,
            FeatureKey = f.Name,
            Values = f.FeatureValues
                .Where(fv => query.Any(p => p.ProductFeatureValues
                    .Any(pfv => pfv.FeatureValueId == fv.Id)))
                .Select(fv => new FilterOptionDto
                {
                    Value = fv.Id,
                    DisplayValue = fv.Name
                })
                .ToList()
        })
        .ToListAsync();

    foreach (var feature in features.Where(f => f.Values.Any()))
    {
        filterDefinitions.Add(new FilterGroupDto
        {
            Key = feature.FeatureKey, // Özellik adını key olarak ata
            Name = feature.FeatureName,
            DisplayName = feature.FeatureName,
            Type = FilterType.Checkbox,
            Options = feature.Values
        });
    }

    // 4. Fiyat Filtresi
    var prices = await query
        .Where(p => p.Price.HasValue)
        .Select(p => p.Price!.Value)
        .ToListAsync();

    if (prices.Any())
    {
        var minPrice = prices.Min();
        var maxPrice = prices.Max();

        filterDefinitions.Add(new FilterGroupDto
        {
            Name = "Price",
            DisplayName = "Price",
            Type = FilterType.Range,
            Options = GeneratePriceRanges(minPrice, maxPrice)
        });
    }

    return filterDefinitions;
}

    private List<FilterOptionDto> GeneratePriceRanges(decimal minPrice, decimal maxPrice)
    {
        var options = new List<FilterOptionDto>();
        var step = (maxPrice - minPrice) / 5;

        for (int i = 0; i < 5; i++)
        {
            var start = minPrice + (step * i);
            var end = i == 4 ? maxPrice : minPrice + (step * (i + 1));

            options.Add(new FilterOptionDto
            {
                Value = $"{start}-{end}",
                DisplayValue = $"{start:N0} - {end:N0} "
            });
        }

        return options;
    }
    
    public async Task<List<Product>> GetBestSellingProducts(int count)
    {
        return await Context.Products
            .AsNoTracking()
            .Include(p => p.ProductImageFiles.Where(pif => pif.Showcase))
            .Include(p => p.Brand)
            .Include(p => p.ProductFeatureValues)
            .ThenInclude(pfv => pfv.FeatureValue)
            .ThenInclude(fv => fv.Feature)
            .Where(p => Context.OrderItems.Any(oi => oi.ProductId == p.Id))
            .OrderByDescending(p => Context.OrderItems
                .Where(oi => oi.ProductId == p.Id)
                .Sum(oi => oi.Quantity))
            .Take(count)
            .ToListAsync();
    }
    
    public async Task<List<Product>> GetMostLikedProductsAsync(int count)
    {
        return await Context.Products
            .AsNoTracking()
            .Include(p => p.ProductLikes)
            .Include(p => p.ProductImageFiles.Where(img => img.Showcase))
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.ProductLikes.Any())
            .OrderByDescending(p => p.ProductLikes.Count)
            .Take(count)
            .ToListAsync();
    }
    public async Task<List<Product>> GetMostViewedProductsAsync(int count)
    {
        return await Context.Products
            .AsNoTracking()
            .Include(p => p.ProductViews)
            .Include(p => p.ProductImageFiles.Where(img => img.Showcase))
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Where(p => p.ProductViews.Any())
            .OrderByDescending(p => p.ProductViews.Count)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Product>> GetRandomProductsAsync(int count)
    {
        return await Context.Products
            .AsNoTracking()
            .Include(p => p.ProductImageFiles.Where(pif => pif.Showcase))
            .OrderBy(x => Guid.NewGuid()) // Random sıralama için
            .Take(count)
            .ToListAsync();
    }
}