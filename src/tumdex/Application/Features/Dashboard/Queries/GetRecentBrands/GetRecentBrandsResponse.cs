using Application.Features.Dashboard.Dtos;

namespace Application.Features.Dashboard.Queries.GetRecentBrands;

public class GetRecentBrandsResponse
{
    public List<RecentItemDto> Brands { get; set; }
}