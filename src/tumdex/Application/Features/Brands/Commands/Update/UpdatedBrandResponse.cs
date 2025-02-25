using Application.Features.Brands.Dtos;
using Core.Application.Responses;

namespace Application.Features.Brands.Commands.Update;

public class UpdatedBrandResponse : IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? OldId { get; set; }
    public BrandImageFileDto? BrandImage { get; set; }
}