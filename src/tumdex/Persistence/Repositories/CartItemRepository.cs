using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class CartItemRepository:EfRepositoryBase<CartItem,string,TumdexDbContext>,ICartItemRepository
{
    public CartItemRepository(TumdexDbContext context) : base(context)
    {
    }
    public async Task<List<(string ProductId, int Count)>> GetMostAddedToCartProductsAsync(int count, DateTime? startDate = null)
    {
        var query = Context.CartItems.AsQueryable();
            
        // Apply time filter if needed
        if (startDate.HasValue)
            query = query.Where(ci => ci.CreatedDate >= startDate.Value);

        // Get top cart products
        var topProducts = await query
            .GroupBy(ci => ci.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(count)
            .ToListAsync();

        return topProducts.Select(tp => (tp.ProductId, tp.Count)).ToList();
    }
}