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
using Persistence.Models;

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

    public async Task<List<FilterGroupDto>> GetAvailableFilters(string? searchTerm = null, string[]? categoryIds = null,
        string[]? brandIds = null)
    {
        var filterDefinitions = new List<FilterGroupDto>();

        // Başlangıç sorgusu oluştur - Kategori ve markayı dahil et
        var query = Context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .AsQueryable();

        // Kategori filtresi uygula
        if (categoryIds != null && categoryIds.Length > 0)
        {
            // Direkt kategorileri ve alt kategorileri ekle
            var allCategoryIds = new HashSet<string>(categoryIds);

            // Alt kategorileri topla
            foreach (var categoryId in categoryIds)
            {
                var subCatIds = await GetAllSubCategoryIds(categoryId);
                foreach (var id in subCatIds)
                {
                    allCategoryIds.Add(id);
                }
            }

            // Kategori filtresi uygula
            query = query.Where(p => allCategoryIds.Contains(p.CategoryId));
        }

        // Arama terimi filtresi
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.SearchByTerm(searchTerm);
        }

        // Marka filtresi
        if (brandIds != null && brandIds.Length > 0)
        {
            query = query.Where(p => brandIds.Contains(p.BrandId));
        }

        // Kategori filtresi
        var categoryFilter = await BuildCategoryFilterHierarchy(query, categoryIds);
        if (categoryFilter.Options.Any())
        {
            filterDefinitions.Add(categoryFilter);
        }

        // Marka filtresi
        var brandFilter = await BuildBrandFilterAsync(query);
        if (brandFilter.Options.Any())
        {
            filterDefinitions.Add(brandFilter);
        }

        // Özellik filtreleri
        var featureFilters = await BuildFeatureFiltersAsync(query);
        filterDefinitions.AddRange(featureFilters);

        // Fiyat filtresi 
        /*var priceFilter = await BuildPriceFilterAsync(query);
        if (priceFilter.Options.Any())
        {
            filterDefinitions.Add(priceFilter);
        }*/

        return filterDefinitions;
    }

// Kategori filtre hiyerarşisini oluşturan yardımcı metot
    private async Task<FilterGroupDto> BuildCategoryFilterHierarchy(IQueryable<Product> query, string[]? categoryIds)
{
    var filter = new FilterGroupDto
    {
        Key = "Category",
        Name = "Category",
        DisplayName = "Category",
        Type = FilterType.Checkbox,
        Options = new List<FilterOptionDto>()
    };

    // Kategori ve alt kategorileri toplayan liste
    var categoryOptions = new List<FilterOptionDto>();

    if (categoryIds != null && categoryIds.Length > 0)
    {
        // Belirli kategoriler için hiyerarşi oluştur (mevcut kod aynı kalır)
        foreach (var categoryId in categoryIds)
        {
            // Ana kategoriyi getir
            var mainCategory = await Context.Categories
                .Where(c => c.Id == categoryId)
                .Select(c => new CategoryInfo
                {
                    Id = c.Id,
                    Name = c.Name,
                    ParentId = c.ParentCategoryId,
                    ProductCount = query.Count(p => p.CategoryId == c.Id)
                })
                .FirstOrDefaultAsync();

            if (mainCategory != null)
            {
                // Ana kategorinin kendisini ekle
                categoryOptions.Add(new FilterOptionDto
                {
                    Value = mainCategory.Id,
                    DisplayValue = mainCategory.Name,
                    ParentId = mainCategory.ParentId,
                    Count = mainCategory.ProductCount
                });

                // Alt kategorileri recursive olarak getir
                var subCategories = await GetAllSubcategoriesRecursive(mainCategory.Id, query);

                // Count > 0 olan alt kategorileri ekle
                foreach (var subCategory in subCategories.Where(sc => sc.Count > 0))
                {
                    categoryOptions.Add(subCategory);
                }
            }
        }
    }
    else
    {
        // Marka veya arama için: Filtrelenmiş ürünlerdeki tüm kategorileri ve hiyerarşilerini al
        var filteredProductCategoryIds = await query
            .Select(p => p.CategoryId)
            .Distinct()
            .ToListAsync();

        if (filteredProductCategoryIds.Any())
        {
            // Önce tüm kategori detaylarını getir
            var allCategories = await Context.Categories
                .Where(c => filteredProductCategoryIds.Contains(c.Id) || 
                           filteredProductCategoryIds.Any(fcid => Context.Categories
                               .Where(subc => subc.Id == fcid)
                               .Select(subc => subc.ParentCategoryId)
                               .Contains(c.Id)))
                .Select(c => new CategoryInfo
                {
                    Id = c.Id,
                    Name = c.Name,
                    ParentId = c.ParentCategoryId,
                    ProductCount = query.Count(p => p.CategoryId == c.Id)
                })
                .ToListAsync();

            // Kök (ana) kategorileri bul
            var rootCategories = allCategories
                .Where(c => c.ParentId == null || !allCategories.Any(ac => ac.Id == c.ParentId))
                .ToList();

            // Her ana kategori için hiyerarşiyi oluştur
            foreach (var rootCategory in rootCategories)
            {
                // Ana kategoriyi ekle
                if (rootCategory.ProductCount > 0 || 
                    allCategories.Any(ac => ac.ParentId == rootCategory.Id && ac.ProductCount > 0))
                {
                    categoryOptions.Add(new FilterOptionDto
                    {
                        Value = rootCategory.Id,
                        DisplayValue = rootCategory.Name,
                        ParentId = rootCategory.ParentId,
                        Count = rootCategory.ProductCount
                    });

                    // Alt kategorileri recursive bir şekilde oluştur
                    BuildCategoryHierarchyRecursive(allCategories, rootCategory.Id, categoryOptions);
                }
            }
        }
    }

    filter.Options = categoryOptions;
    return filter;
}

// Hiyerarşik kategori yapısını oluşturmak için yardımcı metod - CategoryInfo tipini kullan
private void BuildCategoryHierarchyRecursive(List<CategoryInfo> allCategories, string parentId, List<FilterOptionDto> categoryOptions)
{
    var childCategories = allCategories
        .Where(c => c.ParentId == parentId)
        .ToList();

    foreach (var childCategory in childCategories)
    {
        // Sadece ürünü olan kategorileri ekle veya alt kategorilerinde ürün olanları
        if (childCategory.ProductCount > 0 || 
            allCategories.Any(ac => ac.ParentId == childCategory.Id && ac.ProductCount > 0))
        {
            categoryOptions.Add(new FilterOptionDto
            {
                Value = childCategory.Id,
                DisplayValue = childCategory.Name,
                ParentId = childCategory.ParentId,
                Count = childCategory.ProductCount
            });

            // Alt kategoriler için recursive çağrı
            BuildCategoryHierarchyRecursive(allCategories, childCategory.Id, categoryOptions);
        }
    }
}

    private async Task<FilterGroupDto> BuildBrandFilterAsync(IQueryable<Product> query)
    {
        var filter = new FilterGroupDto
        {
            Key = "Brand",
            Name = "Brand",
            DisplayName = "Brand",
            Type = FilterType.Checkbox,
            Options = new List<FilterOptionDto>()
        };

        // Marka filtresi - Count değerlerini ekle ve 0 sayılı markaları filtrele
        var brands = await Context.Brands
            .Select(b => new
            {
                Brand = b,
                Count = query.Count(p => p.BrandId == b.Id)
            })
            .Where(x => x.Count > 0) // ÖNEMLİ: Sadece ürün sayısı > 0 olan markaları getir
            .Select(x => new FilterOptionDto
            {
                Value = x.Brand.Id,
                DisplayValue = x.Brand.Name,
                Count = x.Count
            })
            .ToListAsync();

        filter.Options = brands;
        return filter;
    }

// Özellik filtrelerini oluşturan yardımcı metot
    private async Task<List<FilterGroupDto>> BuildFeatureFiltersAsync(IQueryable<Product> query)
    {
        var featureFilters = new List<FilterGroupDto>();

        // Filtrelenen ürünlerin tüm özellik değerlerini getir
        var productFeatureValues = await query
            .SelectMany(p => p.ProductFeatureValues)
            .Select(pfv => new
            {
                FeatureId = pfv.FeatureValue.FeatureId,
                FeatureValueId = pfv.FeatureValueId,
                FeatureName = pfv.FeatureValue.Feature.Name,
                FeatureValueName = pfv.FeatureValue.Name
            })
            .Distinct()
            .ToListAsync();

        // Özellik değerlerini gruplama ve sayma
        var featureGroups = productFeatureValues
            .GroupBy(pfv => new { pfv.FeatureId, pfv.FeatureName })
            .Select(g => new
            {
                FeatureId = g.Key.FeatureId,
                FeatureName = g.Key.FeatureName,
                Values = g.GroupBy(x => new { x.FeatureValueId, x.FeatureValueName })
                    .Select(vg => new FilterOptionDto
                    {
                        Value = vg.Key.FeatureValueId,
                        DisplayValue = vg.Key.FeatureValueName,
                        Count = query.Count(p => p.ProductFeatureValues
                            .Any(pfv => pfv.FeatureValueId == vg.Key.FeatureValueId))
                    })
                    .Where(v => v.Count > 0)
                    .OrderBy(v => v.DisplayValue)
                    .ToList()
            })
            .Where(f => f.Values.Any())
            .ToList();

        // Her özellik için filtre grubu oluştur
        foreach (var feature in featureGroups)
        {
            featureFilters.Add(new FilterGroupDto
            {
                Key = feature.FeatureName,
                Name = feature.FeatureName,
                DisplayName = feature.FeatureName,
                Type = FilterType.Checkbox,
                Options = feature.Values
            });
        }

        return featureFilters;
    }


    private async Task<List<FilterOptionDto>> GetAllSubcategoriesRecursive(string parentCategoryId,
        IQueryable<Product> query)
    {
        var result = new List<FilterOptionDto>();

        // Doğrudan alt kategorileri getir
        var directSubcategories = await Context.Categories
            .Where(c => c.ParentCategoryId == parentCategoryId)
            .Select(c => new FilterOptionDto
            {
                Value = c.Id,
                DisplayValue = c.Name,
                ParentId = c.ParentCategoryId,
                Count = query.Count(p => p.CategoryId == c.Id)
            })
            .ToListAsync();

        // Doğrudan alt kategorileri ekle
        result.AddRange(directSubcategories);

        // Her alt kategori için recursive çağrı yap
        foreach (var subcategory in directSubcategories)
        {
            var nestedSubcategories = await GetAllSubcategoriesRecursive(subcategory.Value, query);
            result.AddRange(nestedSubcategories);
        }

        return result;
    }

    // Fiyat filtresi oluşturan yardımcı metot
/*private async Task<FilterGroupDto> BuildPriceFilterAsync(IQueryable<Product> query)
{
    var filter = new FilterGroupDto
    {
        Key = "Price",
        Name = "Price",
        DisplayName = "Price",
        Type = FilterType.Range,
        Options = new List<FilterOptionDto>()
    };

    var productPrices = await query
        .Where(p => p.Price.HasValue)
        .Select(p => p.Price!.Value)
        .ToListAsync();

    if (productPrices.Any())
    {
        var minPrice = productPrices.Min();
        var maxPrice = productPrices.Max();
        filter.Options = GeneratePriceRanges(minPrice, maxPrice);
    }

    return filter;
}*/

    /*private List<FilterOptionDto> GeneratePriceRanges(decimal minPrice, decimal maxPrice)
    {
        var options = new List<FilterOptionDto>();

        // Aynı fiyat ise tek bir seçenek göster
        if (minPrice == maxPrice)
        {
            options.Add(new FilterOptionDto
            {
                Value = $"{minPrice}-{maxPrice}",
                DisplayValue = $"{minPrice:N0}",
                Count = 1 // En az bir ürün var
            });
            return options;
        }

        // Fiyat aralıklarını oluştur - 5 aralık
        var step = (maxPrice - minPrice) / 5;

        for (int i = 0; i < 5; i++)
        {
            var start = minPrice + (step * i);
            var end = i == 4 ? maxPrice : minPrice + (step * (i + 1));

            options.Add(new FilterOptionDto
            {
                Value = $"{start}-{end}",
                DisplayValue = $"{start:N0} - {end:N0} ",
                Count = 0 // Count değeri sonradan hesaplanacak
            });
        }

        return options;
    }*/

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
            .Include(p => p.ProductFeatureValues)
            .ThenInclude(pfv => pfv.FeatureValue)
            .ThenInclude(fv => fv.Feature)
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
            .Include(p => p.ProductFeatureValues)
            .ThenInclude(pfv => pfv.FeatureValue)
            .ThenInclude(fv => fv.Feature)
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
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProductFeatureValues)
            .ThenInclude(pfv => pfv.FeatureValue)
            .ThenInclude(fv => fv.Feature)
            .OrderBy(x => Guid.NewGuid()) // Random sıralama için
            .Take(count)
            .ToListAsync();
    }
}