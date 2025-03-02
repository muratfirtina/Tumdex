using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface ICartItemRepository: IAsyncRepository<CartItem,string>, IRepository<CartItem,string>
{
    
}