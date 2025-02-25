using Application.Features.Brands.Dtos;
using Core.Application.Responses;

namespace Application.Features.Brands.Commands.Create;

public class CreatedBrandResponse : IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public BrandImageFileDto? BrandImage { get; set; }
}