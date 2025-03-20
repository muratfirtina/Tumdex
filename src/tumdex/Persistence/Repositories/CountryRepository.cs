using Application.Repositories;
using Core.Persistence.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class CountryRepository : EfRepositoryBase<Country, int, TumdexDbContext>, ICountryRepository
{
    public CountryRepository(TumdexDbContext context) : base(context)
    {
    }

    public async Task<List<Country>> GetAllCountriesAsync()
    {
        return await GetAllAsync(orderBy: x => x.OrderBy(c => c.Name));
    }

    public async Task<Country> GetCountryByIdAsync(int id)
    {
        return await GetAsync(c => c.Id == id);
    }

    public async Task<Country> GetCountryByCodeAsync(string code)
    {
        return await GetAsync(c => c.Code == code);
    }
}