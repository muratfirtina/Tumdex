using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Persistence.Context;

namespace Persistence.Repositories;

public class EndpointRepository : EfRepositoryBase<Endpoint, string, TumdexDbContext>, IEndpointRepository
{
    public EndpointRepository(TumdexDbContext context) : base(context)
    {
    }
}