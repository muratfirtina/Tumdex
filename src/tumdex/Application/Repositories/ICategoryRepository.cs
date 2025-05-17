using Core.Persistence.Paging;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface ICategoryRepository : IAsyncRepository<Category, string>, IRepository<Category, string>
{
    Task<IPaginate<Category>> SearchByNameAsync(string searchTerm);
    Task<List<Category>> GetAllSubCategoriesRecursiveAsync(string parentCategoryId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetTotalProductCountsForCategoriesAsync(List<string> categoryIds, CancellationToken cancellationToken = default);
}