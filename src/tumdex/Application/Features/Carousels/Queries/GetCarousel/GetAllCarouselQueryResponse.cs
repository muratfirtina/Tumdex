using Application.Features.Carousels.Dtos;
using Core.Application.Responses;

namespace Application.Features.Carousels.Queries.GetCarousel;

public class GetAllCarouselQueryResponse :IResponse
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
    public ICollection<CarouselImageFileDto> CarouselImageFiles { get; set; }
}