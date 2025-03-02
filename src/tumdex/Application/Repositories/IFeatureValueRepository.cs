using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface IFeatureValueRepository : IAsyncRepository<FeatureValue, string>, IRepository<FeatureValue, string>
{
    
}