namespace Application.Features.Categories.Dtos;

public class CategoryDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string ParentCategoryId { get; set; }
    public CategoryImageFileDto CategoryImage { get; set; }
    public List<CategoryDto> SubCategories { get; set; }
}