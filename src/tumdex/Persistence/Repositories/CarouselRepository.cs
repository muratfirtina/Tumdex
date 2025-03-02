using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Persistence.Context;

namespace Persistence.Repositories;

public class CarouselRepository : EfRepositoryBase<Carousel, string, TumdexDbContext>, ICarouselRepository
{
    public CarouselRepository(TumdexDbContext context) : base(context)
    {
    }
}