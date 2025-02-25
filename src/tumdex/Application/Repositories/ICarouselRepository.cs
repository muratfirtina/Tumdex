using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface ICarouselRepository : IAsyncRepository<Carousel, string> , IRepository<Carousel, string>
{
    
}