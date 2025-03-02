using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface ICarouselRepository : IAsyncRepository<Carousel, string> , IRepository<Carousel, string>
{
    
}