using Application.Extensions;
using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Core.Application.Responses;

namespace Application.Features.Carts.Queries.GetList;

public class GetCartItemsQueryResponse :IResponse, IHasShowcaseImage
{
    public string CartItemId { get; set; }
    public string? BrandName { get; set; }
    public string? ProductName { get; set; }
    public string? Title { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public int Quantity { get; set; }
    public ProductImageFileDto? ShowcaseImage { get; set; }
    public ICollection<ProductFeatureValueDto>? ProductFeatureValues { get; set; }
    public bool IsChecked { get; set; }
}