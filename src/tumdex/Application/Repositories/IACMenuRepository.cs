using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface IACMenuRepository : IAsyncRepository<ACMenu, string>, IRepository<ACMenu, string>
{
    
}