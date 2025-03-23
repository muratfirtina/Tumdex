using Application.Features.Dashboard.Dtos;

namespace Application.Features.Dashboard.Queries.GetRecentCategories;

public class GetRecentCategoriesResponse
{
    public List<RecentItemDto> Categories { get; set; }
}