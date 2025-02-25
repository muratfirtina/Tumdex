using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface ICartItemRepository: IAsyncRepository<CartItem,string>, IRepository<CartItem,string>
{
    
}