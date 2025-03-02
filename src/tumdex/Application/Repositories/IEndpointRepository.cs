using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface IEndpointRepository : IAsyncRepository<Endpoint, string>, IRepository<Endpoint, string>
{
    
}