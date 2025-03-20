using Core.Persistence.Repositories;
using Domain.Entities;

namespace Application.Repositories;

public interface IDistrictRepository : IAsyncRepository<District, int>, IRepository<District, int>
{
    Task<List<District>> GetAllDistrictsAsync();
    Task<District> GetDistrictByIdAsync(int id);
    Task<List<District>> GetDistrictsByCityIdAsync(int cityId);
}