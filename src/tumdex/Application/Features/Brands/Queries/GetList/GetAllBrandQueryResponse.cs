using Application.Extensions;
using Application.Features.Brands.Dtos;
using Core.Application.Responses;

namespace Application.Features.Brands.Queries.GetList;

public class GetAllBrandQueryResponse : IResponse, IHasBrandImage
{
    public string Id { get; set; } 
    public string Name { get; set; }
    public BrandImageFileDto? BrandImage { get; set; }
    public int ProductCount { get; set; } 
}