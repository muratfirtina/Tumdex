namespace Core.Persistence.Dynamic;

public class DynamicQuery
{
    public IEnumerable<Sort>? Sort { get; set; } = new List<Sort>();
    public Filter? Filter { get; set; }

    public DynamicQuery()
    {
        Sort = new List<Sort>();
    }

    public DynamicQuery(IEnumerable<Sort>? sort, Filter? filter)
    {
        Sort = sort ?? new List<Sort>();
        Filter = filter;
    }
}