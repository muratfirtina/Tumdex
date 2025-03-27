namespace Application.Features.Categories.Dtos;

public class SubCategoryDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? ParentCategoryId { get; set; }
    public CategoryImageFileDto? CategoryImage { get; set; }
    
    // İsteğe bağlı: Alt kategorilere sahip olup olmadığını gösteren bir bayrak
    public bool HasSubCategories { get; set; }
}