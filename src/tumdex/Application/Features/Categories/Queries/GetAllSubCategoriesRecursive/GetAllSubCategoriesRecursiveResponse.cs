using Application.Extensions;
using Application.Features.Categories.Dtos;
using Core.Application.Responses;

namespace Application.Features.Categories.Queries.GetAllSubCategoriesRecursive;

public class GetAllSubCategoriesRecursiveResponse : IResponse, IHasCategoryImage
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? ParentCategoryId { get; set; }
    
    // Kategorinin derinlik seviyesini belirtir (ana kategoriden ne kadar uzakta olduğunu gösterir)
    public int Depth { get; set; }
    
    // Alt kategorilerin doğrudan referansını tutmak için (UI'da göstermek gerekirse)
    public ICollection<SubCategoryDto>? DirectSubCategories { get; set; }
    
    // Kategori görseli
    public CategoryImageFileDto? CategoryImage { get; set; }
    
    // Diğer gerekli bilgiler (örn. ürün sayısı gibi ekstra bilgiler ekleyebilirsiniz)
    public int ProductCount { get; set; }
}