using Application.Extensions;
using Application.Features.Categories.Dtos;
using Core.Application.Responses;

namespace Application.Features.Categories.Queries.GetMainCategories;

public class GetMainCategoriesResponse : IResponse, IHasCategoryImage
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Title { get; set; }
    public int? ProductCount { get; set; }
    public CategoryImageFileDto? CategoryImage { get; set; }
}