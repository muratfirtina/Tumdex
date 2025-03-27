using Domain;
using Domain.Entities;
using Domain.Enum;

namespace Application.Features.Products.Dtos.FilterDto;

public class FilterGroupDto
{
    public string Key { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public FilterType Type { get; set; }
    public List<FilterOptionDto> Options { get; set; } = new List<FilterOptionDto>();
}