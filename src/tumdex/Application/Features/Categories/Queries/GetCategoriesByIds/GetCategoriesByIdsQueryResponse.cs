using Application.Extensions;
using Application.Features.Categories.Dtos;
using Core.Application.Responses;

namespace Application.Features.Categories.Queries.GetCategoriesByIds;

public class GetCategoriesByIdsQueryResponse :IResponse, IHasCategoryImage
{
    public string Id { get; set; } 
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? ParentCategoryId { get; set; }
    public ICollection<GetListSubCategoryDto>? SubCategories { get; set; }
    public CategoryImageFileDto? CategoryImage { get; set; }
}