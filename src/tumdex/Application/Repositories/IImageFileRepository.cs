using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IImageFileRepository :IAsyncRepository<ImageFile, string>, IRepository<ImageFile, string>
{
    
}