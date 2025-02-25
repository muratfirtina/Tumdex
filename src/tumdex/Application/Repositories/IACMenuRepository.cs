using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IACMenuRepository : IAsyncRepository<ACMenu, string>, IRepository<ACMenu, string>
{
    
}