using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IProductLikeRepository : IAsyncRepository<ProductLike, string>, IRepository<ProductLike, string>
{
    Task<ProductLike> AddProductLikeAsync(string productId);
    Task<ProductLike> RemoveProductLikeAsync(string productId);
    Task<IPaginate<ProductLike>> GetUserLikedProductsAsync(int pageIndex , int pageSize , CancellationToken cancellationToken = default);
    
    Task<List<string>> GetUserLikedProductIdsAsync(string searchProductIdsString);// Yeni eklenen metod

    Task<bool> IsProductLikedAsync(string productId);
    Task<int> GetProductLikeCountAsync(string productId);
    Task<List<string>> GetMostLikedProductsAsync(int count);


}