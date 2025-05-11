namespace Application.Features.Products.Dtos.FilterDto;

public class FilterOptionDto
{
    public string? Value { get; set; }
    public string? DisplayValue { get; set; }
    public string? ParentId { get; set; }
    public int Count { get; set; }
}