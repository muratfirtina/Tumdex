using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Persistence.Context;

namespace Persistence.Repositories;

public class CartRepository:EfRepositoryBase<Cart,string,TumdexDbContext>,ICartRepository
{
    public CartRepository(TumdexDbContext context) : base(context)
    {
    }
}