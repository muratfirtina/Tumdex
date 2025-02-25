using System.ComponentModel.DataAnnotations.Schema;
using Core.Persistence.Repositories;

namespace Domain;

public class FilterOption : Entity<string>
{
    public string FilterGroupId { get; set; }
    public FilterGroup FilterGroup { get; set; }
    public string Value { get; set; }
    public string DisplayValue { get; set; }
    [NotMapped]
    public string ParentId { get; set; }
}