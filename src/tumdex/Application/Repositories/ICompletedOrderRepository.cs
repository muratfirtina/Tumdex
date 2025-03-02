using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface ICompletedOrderRepository: IAsyncRepository<CompletedOrder, string>, IRepository<CompletedOrder, string>
{
    
}