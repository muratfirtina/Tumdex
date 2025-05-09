using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Application.Features.Products.Dtos.FilterDto;
using Core.Application.Requests;
using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface IProductRepository : IAsyncRepository<Product, string>, IRepository<Product, string>
{
    Task<List<ProductImageFileDto>> GetFilesByProductId(string productId,string? preferredStorage = null);
    Task ChangeShowcase(string productId, string imageFileId,bool showcase);
    Task<ProductImageFile?> GetProductImage(string productId);

    //bir sözcük veya kelimeyi içeren ürünleri pagination ile getirir
    Task<(IPaginate<Product>, List<Category>, List<Brand>)> SearchProductsAsync(string? searchTerm, int pageIndex, int pageSize);
    
    Task<IPaginate<Product>> FilterProductsAsync(string? searchTerm,Dictionary<string, List<string>>? filters, PageRequest? pageRequest,string sortOrder);
    Task<List<FilterGroupDto>> GetAvailableFilters(string? searchTerm = null, string[]? categoryIds = null, string[]? brandIds = null);
    Task<List<Product>> GetBestSellingProducts(int count);
    Task<List<Product>> GetMostLikedProductsAsync(int count);
    
    Task<List<Product>> GetMostViewedProductsAsync(int count);
    Task<List<Product>> GetRandomProductsAsync(int count);
    

}