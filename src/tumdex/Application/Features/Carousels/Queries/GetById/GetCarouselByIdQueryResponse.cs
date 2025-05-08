using Application.Features.Carousels.Dtos;
using Core.Application.Responses;

namespace Application.Features.Carousels.Queries.GetById;

public class GetCarouselByIdQueryResponse : IResponse
{
    public string Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
    
    // Video özellikleri
    public string? MediaType { get; set; }
    public string? VideoType { get; set; }
    public string? VideoUrl { get; set; }
    public string? VideoId { get; set; }
    
    public ICollection<CarouselImageFileDto> CarouselImageFiles { get; set; }
}