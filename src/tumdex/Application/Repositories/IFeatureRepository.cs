using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IFeatureRepository : IAsyncRepository<Feature, string>, IRepository<Feature, string>
{
    
}