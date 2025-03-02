using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Persistence.Context;

namespace Persistence.Repositories;

public class CompletedOrderRepository:EfRepositoryBase<CompletedOrder,string,TumdexDbContext>,ICompletedOrderRepository
{
    public CompletedOrderRepository(TumdexDbContext context) : base(context)
    {
    }
}