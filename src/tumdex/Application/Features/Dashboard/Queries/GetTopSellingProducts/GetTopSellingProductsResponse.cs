using Application.Features.Dashboard.Dtos;

namespace Application.Features.Dashboard.Queries.GetTopSellingProducts;

public class GetTopSellingProductsResponse
{
    public List<TopProductDto> Products { get; set; }
}