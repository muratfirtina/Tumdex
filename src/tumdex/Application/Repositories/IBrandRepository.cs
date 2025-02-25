using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IBrandRepository : IAsyncRepository<Brand, string>, IRepository<Brand, string>
{
    Task<IPaginate<Brand>> SearchByNameAsync(string searchTerm);
}