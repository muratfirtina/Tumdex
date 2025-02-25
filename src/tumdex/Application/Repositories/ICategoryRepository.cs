using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface ICategoryRepository : IAsyncRepository<Category, string>, IRepository<Category, string>
{
    Task<IPaginate<Category>> SearchByNameAsync(string searchTerm);
}