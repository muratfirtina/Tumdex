using Core.Persistence.Repositories;
using Domain.Entities;

namespace Application.Repositories;

public interface ICountryRepository : IAsyncRepository<Country, int>, IRepository<Country, int>
{
    Task<List<Country>> GetAllCountriesAsync();
    Task<Country> GetCountryByIdAsync(int id);
    Task<Country> GetCountryByCodeAsync(string code);
}