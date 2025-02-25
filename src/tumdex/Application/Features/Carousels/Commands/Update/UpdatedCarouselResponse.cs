using Application.Features.Carousels.Dtos;
using Core.Application.Responses;

namespace Application.Features.Carousels.Commands.Update;

public class UpdatedCarouselResponse : IResponse
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Order { get; set; }
    public List<CarouselImageFileDto>? CarouselImageFiles { get; set; }
}