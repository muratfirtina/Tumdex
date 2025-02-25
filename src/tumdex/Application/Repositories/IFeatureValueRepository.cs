using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IFeatureValueRepository : IAsyncRepository<FeatureValue, string>, IRepository<FeatureValue, string>
{
    
}