namespace Application.Features.Products.Dtos;

public class ProductImageSeoDto
{
    public string? AltText { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Caption { get; set; }
    public int ImageIndex { get; set; } // Hangi görsele ait olduğunu belirtmek için
}