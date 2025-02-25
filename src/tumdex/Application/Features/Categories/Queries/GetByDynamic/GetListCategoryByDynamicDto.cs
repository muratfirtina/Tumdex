using Application.Extensions;
using Application.Features.Categories.Dtos;
using Application.Features.Products.Dtos;

namespace Application.Features.Categories.Queries.GetByDynamic;

public class GetListCategoryByDynamicDto : IHasCategoryImage
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Title { get; set; }
    public List<GetListCategoryByDynamicDto> SubCategories { get; set; } = new List<GetListCategoryByDynamicDto>();
    public List<ProductDto> Products { get; set; } = new List<ProductDto>();
    public CategoryImageFileDto? CategoryImage { get; set; }
}