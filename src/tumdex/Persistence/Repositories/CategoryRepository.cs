using Application.Repositories;
using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class CategoryRepository : EfRepositoryBase<Category, string, TumdexDbContext>, ICategoryRepository
{
    public CategoryRepository(TumdexDbContext context) : base(context)
    {
    }
    
    public async Task<IPaginate<Category>> SearchByNameAsync(string searchTerm)
    {
        var query = Context.Categories
            .Include(c => c.CategoryImageFiles)
            .Include(c => c.SubCategories)
            .ThenInclude(sc => sc.CategoryImageFiles)
            .AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{searchTerm}%"));
        }

        return await query.ToPaginateAsync(0, 10);
    }
    
    public async Task<List<Category>> GetAllSubCategoriesRecursiveAsync(string parentCategoryId, CancellationToken cancellationToken = default)
    {
        // Ana kategoriyi gerekli ilişkileriyle birlikte yükle
        var rootCategory = await Context.Categories
            .Include(c => c.CategoryImageFiles)
            .Include(c => c.Features)
            .ThenInclude(f => f.FeatureValues)
            .FirstOrDefaultAsync(c => c.Id == parentCategoryId, cancellationToken);
    
        if (rootCategory == null)
            return new List<Category>();

        // Tüm kategorileri depolayacak koleksiyon
        List<Category> allCategories = new List<Category>();
        allCategories.Add(rootCategory);

        // Alt kategorileri rekursif şekilde getiren yardımcı metod
        async Task CollectSubCategoriesAsync(string categoryId, int depth = 0, int maxDepth = 20)
        {
            if (depth >= maxDepth) // Sonsuz döngüyü önlemek için maksimum derinlik sınırı
                return;

            // Bu kategorinin doğrudan alt kategorilerini gerekli ilişkileriyle birlikte getir
            var subCategories = await Context.Categories
                .Include(c => c.CategoryImageFiles)
                .Include(c => c.Features)
                .ThenInclude(f => f.FeatureValues)
                .Where(c => c.ParentCategoryId == categoryId)
                .ToListAsync(cancellationToken);

            // Alt kategorileri ana listeye ekle
            allCategories.AddRange(subCategories);

            // Her bir alt kategori için, onun alt kategorilerini rekursif olarak getir
            foreach (var subCategory in subCategories)
            {
                await CollectSubCategoriesAsync(subCategory.Id, depth + 1, maxDepth);
            }
        }

        // Ana kategorinin tüm alt kategorilerini topla
        await CollectSubCategoriesAsync(parentCategoryId);

        return allCategories;
    }
}