using Application.Extensions;
using Application.Features.Brands.Dtos;
using Core.Application.Responses;

namespace Application.Features.Brands.Queries.GetById;

public class GetByIdBrandResponse : IResponse, IHasBrandImage
{
    public string Id { get; set; }
    public string Name { get; set; }
    public BrandImageFileDto? BrandImage { get; set; }
}