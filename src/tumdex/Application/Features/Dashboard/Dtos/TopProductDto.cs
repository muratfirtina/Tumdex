namespace Application.Features.Dashboard.Dtos;

public class TopProductDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Count { get; set; }
    public string Image { get; set; }
    public decimal Price { get; set; }
    public string BrandName { get; set; }
}