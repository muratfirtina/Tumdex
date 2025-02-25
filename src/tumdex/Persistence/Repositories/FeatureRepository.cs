using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Persistence.Context;

namespace Persistence.Repositories;

public class FeatureRepository : EfRepositoryBase<Feature, string, TumdexDbContext>, IFeatureRepository
{
    public FeatureRepository(TumdexDbContext context) : base(context)
    {
    }
}