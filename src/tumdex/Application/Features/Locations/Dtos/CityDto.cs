using Core.Application.Responses;

namespace Application.Features.Locations.Dtos;

public class CityDto : IResponse
{
    public int Id { get; set; }
    public int CountryId { get; set; }
    public string Name { get; set; }
    public string? Code { get; set; }
}