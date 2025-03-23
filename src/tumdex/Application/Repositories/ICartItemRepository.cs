using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface ICartItemRepository: IAsyncRepository<CartItem,string>, IRepository<CartItem,string>
{
    Task<List<(string ProductId, int Count)>> GetMostAddedToCartProductsAsync(int count, DateTime? startDate = null);
}