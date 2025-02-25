namespace Application.Features.ProductImageFiles.Dtos;

public class ProductImageFileDto
{
    public string Id { get; set; }
    public string? FileName { get; set; }
    public string Path { get; set; }
    public string EntityType { get; set; }
    public string Storage { get; set; }
    public string? Alt { get; set; }
    public string Url { get; set; }
    public bool? Showcase { get; set; }
}