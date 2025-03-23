using Application.Features.Dashboard.Dtos;

namespace Application.Features.Dashboard.Queries.GetTopCartProducts;

public class GetTopCartProductsResponse
{
    public List<TopProductDto> Products { get; set; }
}