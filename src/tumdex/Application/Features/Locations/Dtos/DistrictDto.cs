using Core.Application.Responses;

namespace Application.Features.Locations.Dtos;

public class DistrictDto : IResponse
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public string Name { get; set; }
    public string? Code { get; set; }
}