using Application.Extensions;
using Application.Features.Brands.Dtos;
using Core.Application.Responses;

namespace Application.Features.Brands.Queries.GetByDynamic;

public class GetListBrandByDynamicQueryResponse : IResponse, IHasBrandImage
{
    public string Id { get; set; } 
    public string Name { get; set; }
    public BrandImageFileDto? BrandImage { get; set; }

}