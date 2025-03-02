using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface IFeatureRepository : IAsyncRepository<Feature, string>, IRepository<Feature, string>
{
    
}