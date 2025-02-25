using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface ICartRepository : IAsyncRepository<Cart, string>, IRepository<Cart, string>
{
    
}