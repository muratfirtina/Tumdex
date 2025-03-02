using Core.Persistence.Repositories;
using Domain.Enum;

namespace Domain.Entities;

public class FilterGroup : Entity<string>
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public FilterType Type { get; set; }
    public ICollection<FilterOption> Options { get; set; }
}