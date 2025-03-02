using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Application.Features.Carousels.Dtos;
using Application.Repositories;
using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class ProductLikeRepository : EfRepositoryBase<ProductLike, string, TumdexDbContext>, IProductLikeRepository
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<AppUser> _userManager;

    public ProductLikeRepository(TumdexDbContext context, IHttpContextAccessor httpContextAccessor, UserManager<AppUser> userManager) 
        : base(context)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    private async Task<AppUser?> GetCurrentUser()
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userName))
        {
            return await _userManager.FindByNameAsync(userName);
        }
        return null;
    }

    public async Task<IPaginate<ProductLike>> GetUserLikedProductsAsync(int index, int size, CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUser();
        if (user == null)
        {
            return null;
        }

        var query = Context.ProductLikes
            .Include(pl => pl.Product)
            .ThenInclude(p => p.Brand)
            .Include(pl => pl.Product)
            .ThenInclude(p => p.Category)
            .Include(pl => pl.Product)
            .ThenInclude(p => p.ProductImageFiles)
            .Include(pl => pl.Product)
            .ThenInclude(p => p.ProductFeatureValues)
            .ThenInclude(pfv => pfv.FeatureValue)
            .ThenInclude(fv => fv.Feature)
            .Where(pl => pl.UserId == user.Id)
            .OrderByDescending(pl => pl.LikeDate);

        return await query.ToPaginateAsync(index, size, 0, cancellationToken);
    }

    public async Task<ProductLike> AddProductLikeAsync(string productId)
    {
        var user = await GetCurrentUser();
        if (user == null)
        {
            throw new Exception("User not found.");
        }

        var productLike = await GetAsync(pl => pl.ProductId == productId && pl.UserId == user.Id);
        
        productLike = new ProductLike
        {
            Id = Guid.NewGuid().ToString(),
            ProductId = productId,
            UserId = user.Id,
            LikeDate = DateTime.Now
        };
        
        return await AddAsync(productLike);
    }

    public async Task<ProductLike> RemoveProductLikeAsync(string productId)
    {
        var user = await GetCurrentUser();
        if (user == null)
        {
            throw new Exception("User not found.");
        }

        var productLike = await Context.ProductLikes
            .FirstOrDefaultAsync(pl => pl.ProductId == productId && pl.UserId == user.Id);

        Context.ProductLikes.Remove(productLike); // Hard delete
        await Context.SaveChangesAsync();
        
        return productLike;
    }

    public async Task<bool> IsProductLikedAsync(string productId)
    {
        var user = await GetCurrentUser();
        if (user == null)
        {
            return false;
        }

        return await AnyAsync(pl => pl.ProductId == productId && pl.UserId == user.Id);
    }

    public async Task<List<string>?> GetUserLikedProductIdsAsync(string? searchProductIdsString)
    {
        // searchProductIdsString null veya boş ise null dön
        if (string.IsNullOrWhiteSpace(searchProductIdsString))
        {
            return null;
        }

        // Gelen string'i ayrı ID'lere bölelim
        var searchProductIds = searchProductIdsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => id.Trim())
            .ToList();

        // Eğer bölme işleminden sonra liste boş ise null dön
        if (!searchProductIds.Any())
        {
            return null;
        }

        var user = await GetCurrentUser();
        if (user == null)
        {
            return null;
        }

        var likedProductIds = await Context.ProductLikes
            .Where(pl => pl.UserId == user.Id)
            .Where(pl => searchProductIds.Contains(pl.ProductId))
            .Select(pl => pl.ProductId)
            .ToListAsync();

        return likedProductIds;
    }
    
    public async Task<int> GetProductLikeCountAsync(string productId)
    {
        return await Context.ProductLikes
            .Where(pl => pl.ProductId == productId)
            .CountAsync();
    }
    public async Task<List<string>> GetMostLikedProductsAsync(int count)
    {
        return await Context.ProductLikes
            .GroupBy(pl => pl.ProductId)
            .Select(g => new { ProductId = g.Key, LikeCount = g.Count() })
            .OrderByDescending(x => x.LikeCount)
            .Where(x => x.LikeCount > 0)
            .Take(count)
            .Select(x => x.ProductId)
            .ToListAsync();
    }

}