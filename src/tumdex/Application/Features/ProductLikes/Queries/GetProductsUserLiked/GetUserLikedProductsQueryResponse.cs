using Application.Features.ProductImageFiles.Dtos;
using Application.Features.Products.Dtos;
using Core.Application.Responses;

namespace Application.Features.ProductLikes.Queries.GetProductsUserLiked;

public class GetUserLikedProductsQueryResponse : IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string CategoryId { get; set; }
    public string CategoryName { get; set; }
    public string BrandId { get; set; }
    public string BrandName { get; set; }
    public string Sku { get; set; }
    public string VaryantGroupID { get; set; }
    public int Stock{ get; set; }
    public decimal? Price { get; set; }
    public ICollection<ProductFeatureValueDto>? ProductFeatureValues { get; set; }
    public ProductImageFileDto? ShowcaseImage { get; set; }
}