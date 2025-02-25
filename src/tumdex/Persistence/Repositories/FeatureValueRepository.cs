using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Persistence.Context;

namespace Persistence.Repositories;

public class FeatureValueRepository : EfRepositoryBase<FeatureValue, string, TumdexDbContext>, IFeatureValueRepository
{
    public FeatureValueRepository(TumdexDbContext context) : base(context)
    {
    }
}