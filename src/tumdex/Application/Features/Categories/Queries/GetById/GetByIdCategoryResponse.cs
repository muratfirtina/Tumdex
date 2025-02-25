using Application.Extensions;
using Application.Features.Categories.Dtos;
using Application.Features.Features.Dtos;
using Core.Application.Responses;

namespace Application.Features.Categories.Queries.GetById;

public class GetByIdCategoryResponse : IResponse, IHasCategoryImage
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public ICollection<GetListSubCategoryDto>? SubCategories { get; set; }
    public ICollection<FeatureDto>? Features { get; set; }
    public Dictionary<string, Dictionary<string, int>> FeatureValueProductCounts { get; set; }
    public CategoryImageFileDto CategoryImage { get; set; }

    
}