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
}