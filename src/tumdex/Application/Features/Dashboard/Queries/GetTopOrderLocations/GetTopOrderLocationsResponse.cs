using Application.Features.Dashboard.Dtos;

namespace Application.Features.Dashboard.Queries.GetTopOrderLocations;

public class GetTopOrderLocationsResponse
{
    public List<TopLocationDto> Locations { get; set; }
}