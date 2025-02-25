using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Persistence.Context;

namespace Persistence.Repositories;

public class ACMenuRepository : EfRepositoryBase<ACMenu, string, TumdexDbContext>, IACMenuRepository
{
    public ACMenuRepository(TumdexDbContext context) : base(context)
    {
    }
}