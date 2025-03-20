using Application.Repositories;
using Core.Persistence.Repositories;
using Domain.Entities;
using Persistence.Context;

namespace Persistence.Repositories;

public class CityRepository : EfRepositoryBase<City, int, TumdexDbContext>, ICityRepository
{
    public CityRepository(TumdexDbContext context) : base(context)
    {
    }

    public async Task<List<City>> GetAllCitiesAsync()
    {
        return await GetAllAsync(orderBy: x => x.OrderBy(c => c.Name));
    }

    public async Task<City> GetCityByIdAsync(int id)
    {
        return await GetAsync(c => c.Id == id);
    }

    public async Task<List<City>> GetCitiesByCountryIdAsync(int countryId)
    {
        return await GetAllAsync(
            predicate: c => c.CountryId == countryId,
            orderBy: x => x.OrderBy(c => c.Name)
        );
    }
}