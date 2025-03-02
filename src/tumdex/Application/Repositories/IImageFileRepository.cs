using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface IImageFileRepository :IAsyncRepository<ImageFile, string>, IRepository<ImageFile, string>
{
    
}