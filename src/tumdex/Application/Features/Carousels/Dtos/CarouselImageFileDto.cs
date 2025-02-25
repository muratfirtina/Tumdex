namespace Application.Features.Carousels.Dtos;

public class CarouselImageFileDto
{
    public string Id { get; set; }
    public string? FileName { get; set; }
    public string Path { get; set; }
    public string EntityType { get; set; }
    public string Storage { get; set; }
    public string? Alt { get; set; }
    public string Url { get; set; }
}