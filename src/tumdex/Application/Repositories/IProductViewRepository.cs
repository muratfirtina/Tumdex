using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IProductViewRepository : IAsyncRepository<ProductView, string>, IRepository<ProductView, string>
{
    Task TrackProductView(string productId);
}