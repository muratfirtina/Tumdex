using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IEndpointRepository : IAsyncRepository<Endpoint, string>, IRepository<Endpoint, string>
{
    
}