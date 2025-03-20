using Core.Persistence.Repositories;
using Domain.Entities;

namespace Application.Repositories;

public interface ICityRepository : IAsyncRepository<City, int>, IRepository<City, int>
{
    Task<List<City>> GetAllCitiesAsync();
    Task<City> GetCityByIdAsync(int id);
    Task<List<City>> GetCitiesByCountryIdAsync(int countryId);
}