using Application.Extensions;
using Application.Features.Categories.Dtos;
using Core.Application.Responses;
using Domain;

namespace Application.Features.Categories.Queries.GetList;

public class GetAllCategoryQueryResponse : IResponse, IHasCategoryImage
{
    public string Id { get; set; } 
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? ParentCategoryId { get; set; }
    public int? ProductCount { get; set; }
    public ICollection<GetListSubCategoryDto>? SubCategories { get; set; }
    public CategoryImageFileDto? CategoryImage { get; set; }
}