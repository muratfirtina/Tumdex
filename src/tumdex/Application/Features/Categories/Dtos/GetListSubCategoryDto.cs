namespace Application.Features.Categories.Dtos;

public class GetListSubCategoryDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? ParentCategoryId { get; set; }
    public List<GetListSubCategoryDto>? SubCategories { get; set; }
    public CategoryImageFileDto? CategoryImage { get; set; }
}