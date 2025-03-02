using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class ProductViewRepository : EfRepositoryBase<ProductView, string, TumdexDbContext>, IProductViewRepository
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<AppUser> _userManager;

    public ProductViewRepository(
        TumdexDbContext context,
        IHttpContextAccessor httpContextAccessor,
        UserManager<AppUser> userManager) : base(context)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userName))
        {
            AppUser? user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == userName);

            if (user == null)
            {
                throw new Exception("User not found.");
            }

            return user;
        }

        return null;
    }

    public async Task TrackProductView(string productId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return; // Kullanıcı giriş yapmamışsa işlemi kaydetme

        var productView = new ProductView
        {
            ProductId = productId,
            UserId = user.Id,
            VisitDate = DateTime.UtcNow
        };

        await AddAsync(productView);
    }
}