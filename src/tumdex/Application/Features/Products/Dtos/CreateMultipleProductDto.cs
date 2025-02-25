using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Application.Features.Products.Dtos;

public class CreateMultipleProductDto
{
    public string Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string CategoryId { get; set; }
    public string BrandId { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int Stock { get; set; }
    public int? Tax { get; set; }
    public string? VaryantGroupID { get; set; }
    public List<string>? FeatureIds { get; set; }
    public List<string>? FeatureValueIds { get; set; }
    [JsonIgnore]
    public List<IFormFile>? ProductImages { get; set; }
    public int? ShowcaseImageIndex { get; set; } 

}