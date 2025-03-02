using Application.Repositories;
using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class BrandRepository : EfRepositoryBase<Brand, string, TumdexDbContext>, IBrandRepository
{
    public BrandRepository(TumdexDbContext context) : base(context)
    {
    }
    
    public async Task<IPaginate<Brand>> SearchByNameAsync(string searchTerm)
    {
        var query = Context.Brands
            .Include(b => b.BrandImageFiles)
            .AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(b => EF.Functions.ILike(b.Name, $"%{searchTerm}%"));
        }

        return await query.ToPaginateAsync(0, 10);
    }
}