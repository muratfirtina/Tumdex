using Domain;
using Domain.Enum;

namespace Application.Features.Products.Dtos.FilterDto;

public class FilterGroupDto
{
    public string Key { get; set; }
    public string DisplayName { get; set; }
    public FilterType Type { get; set; }
    public ICollection<FilterOption> Options { get; set; }
}