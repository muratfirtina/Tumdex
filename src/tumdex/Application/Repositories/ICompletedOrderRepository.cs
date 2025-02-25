using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface ICompletedOrderRepository: IAsyncRepository<CompletedOrder, string>, IRepository<CompletedOrder, string>
{
    
}