using Application.Repositories;
using Core.Persistence.Repositories;
using Domain.Entities;
using Persistence.Context;

namespace Persistence.Repositories;

public class DistrictRepository : EfRepositoryBase<District, int, TumdexDbContext>, IDistrictRepository
{
    public DistrictRepository(TumdexDbContext context) : base(context)
    {
    }

    public async Task<List<District>> GetAllDistrictsAsync()
    {
        return await GetAllAsync(orderBy: x => x.OrderBy(d => d.Name));
    }

    public async Task<District> GetDistrictByIdAsync(int id)
    {
        return await GetAsync(d => d.Id == id);
    }

    public async Task<List<District>> GetDistrictsByCityIdAsync(int cityId)
    {
        return await GetAllAsync(
            predicate: d => d.CityId == cityId,
            orderBy: x => x.OrderBy(d => d.Name)
        );
    }
}