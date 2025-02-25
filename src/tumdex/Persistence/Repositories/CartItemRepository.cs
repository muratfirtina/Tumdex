using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Persistence.Context;

namespace Persistence.Repositories;

public class CartItemRepository:EfRepositoryBase<CartItem,string,TumdexDbContext>,ICartItemRepository
{
    public CartItemRepository(TumdexDbContext context) : base(context)
    {
    }
}