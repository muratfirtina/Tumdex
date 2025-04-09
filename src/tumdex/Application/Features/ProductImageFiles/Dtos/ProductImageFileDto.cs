using Application.Dtos.Image;

namespace Application.Features.ProductImageFiles.Dtos;

public class ProductImageFileDto : ImageFileDto
{
    public bool? Showcase { get; set; }
}