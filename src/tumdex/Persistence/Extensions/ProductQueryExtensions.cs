using Domain;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Extensions;

public static class ProductQueryExtensions
{
    public static IQueryable<Product> WithBasicIncludes(
        this IQueryable<Product> query)
    {
        return query
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProductLikes);
    }

    public static IQueryable<Product> WithFullDetails(
        this IQueryable<Product> query)
    {
        return query
            .WithBasicIncludes()
            .Include(p => p.ProductFeatureValues)
                .ThenInclude(pfv => pfv.FeatureValue)
                .ThenInclude(fv => fv.Feature)
            .Include(p => p.ProductImageFiles)
            .AsSplitQuery();
    }

    public static IQueryable<Product> WithShowcaseImage(
        this IQueryable<Product> query,
        bool onlyShowcase = true)
    {
        return onlyShowcase
            ? query.Include(p => p.ProductImageFiles.Where(pif => pif.Showcase))
            : query.Include(p => p.ProductImageFiles);
    }

    public static IQueryable<Product> SearchByTerm(this IQueryable<Product> query, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var lowerSearchTerm = searchTerm.ToLower();

        return query.Where(p =>
            p.Name.ToLower().Contains(lowerSearchTerm) ||
            p.Description.ToLower().Contains(lowerSearchTerm) ||
            p.Title.ToLower().Contains(lowerSearchTerm) ||
            p.Category.Name.ToLower().Contains(lowerSearchTerm) ||
            p.Brand.Name.ToLower().Contains(lowerSearchTerm)
        );
    }

    public static IQueryable<Product> ApplyFilters(
        this IQueryable<Product> query,
        Dictionary<string, List<string>> filters)
    {
        foreach (var filter in filters.Where(f => f.Value.Count > 0))
        {
            query = filter.Key switch
            {
                "Brand" => query.Where(p => filter.Value.Contains(p.Brand.Id)),
                "Category" => query.Where(p => filter.Value.Contains(p.CategoryId)),
                "Price" when !string.IsNullOrWhiteSpace(filter.Value[0]) =>
                    ApplyPriceFilter(query, filter.Value[0]),
                _ => ApplyFeatureFilter(query, filter.Key, filter.Value)
            };
        }
        return query;
    }

    private static IQueryable<Product> ApplyPriceFilter(
        IQueryable<Product> query, 
        string priceRange)
    {
        var range = priceRange.Split('-');
        if (range.Length != 2) return query;

        if (decimal.TryParse(range[0], out decimal minPrice))
            query = query.Where(p => p.Price >= minPrice);
        
        if (decimal.TryParse(range[1], out decimal maxPrice))
            query = query.Where(p => p.Price <= maxPrice);

        return query;
    }

    private static IQueryable<Product> ApplyFeatureFilter(
        IQueryable<Product> query,
        string featureName,
        List<string> featureValues)
    {
        return query.Where(p => p.ProductFeatureValues.Any(pfv =>
            pfv.FeatureValue.Feature.Name == featureName &&
            featureValues.Contains(pfv.FeatureValue.Id)));
    }

    public static IQueryable<Product> ApplySort(
        this IQueryable<Product> query,
        string sortOrder)
    {
        return sortOrder switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            _ => query.OrderByDescending(p => p.CreatedDate)
        };
    }
    
    public static async Task<(List<Product>, List<Category>, List<Brand>)> SearchAllAsync(
        this IQueryable<Product> products,
        IQueryable<Category> categories,
        IQueryable<Brand> brands,
        string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return (new List<Product>(), new List<Category>(), new List<Brand>());

        var lowerSearchTerm = searchTerm.ToLower();
        var terms = lowerSearchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Ürünleri ara
        var matchingProducts = await products
            .Where(p => terms.All(term =>
                EF.Functions.ILike(p.Name, $"%{term}%") ||
                EF.Functions.ILike(p.Description, $"%{term}%") ||
                EF.Functions.ILike(p.Title, $"%{term}%") ||
                EF.Functions.ILike(p.Category.Name, $"%{term}%") ||
                EF.Functions.ILike(p.Brand.Name, $"%{term}%")))
            .AsNoTracking()
            .ToListAsync();

        // Kategorileri ara
        var matchingCategories = await categories
            .Where(c => terms.All(term =>
                EF.Functions.ILike(c.Name, $"%{term}%")))
            .AsNoTracking()
            .ToListAsync();

        // Markaları ara
        var matchingBrands = await brands
            .Where(b => terms.All(term =>
                EF.Functions.ILike(b.Name, $"%{term}%")))
            .AsNoTracking()
            .ToListAsync();

        return (matchingProducts, matchingCategories, matchingBrands);
    }
}