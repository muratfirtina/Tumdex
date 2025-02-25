using System.Linq.Dynamic.Core;
using System.Text;

namespace Core.Persistence.Dynamic;

public static class IQueryableDynamicFilterExtensions
{
    private static readonly string[] _orders = { "asc", "desc" };
    private static readonly string[] _logics = { "and", "or" };

    private static readonly IDictionary<string, string> _operators = new Dictionary<string, string>
    {
        { "eq", "=" },
        { "neq", "!=" },
        { "lt", "<" },
        { "lte", "<=" },
        { "gt", ">" },
        { "gte", ">=" },
        { "isnull", "== null" },
        { "isnotnull", "!= null" },
        { "startswith", "StartsWith" },
        { "endswith", "EndsWith" },
        { "contains", "Contains" },
        { "doesnotcontain", "Contains" }
    };

    public static IQueryable<T> ToDynamic<T>(this IQueryable<T> query, DynamicQuery? dynamicQuery)
    {
        if (dynamicQuery == null)
            return query;
        
        if (dynamicQuery.Filter != null)
            query = Filter(query, dynamicQuery.Filter);
        
        if (dynamicQuery.Sort?.Any() == true)
            query = Sort(query, dynamicQuery.Sort);
        
        return query;
    }

    private static IQueryable<T> Filter<T>(IQueryable<T> queryable, Filter filter)
    {
        
        IList<Filter> filters = GetAllFilters(filter);
        string?[] values = filters.Select(f => f.Value).ToArray();
        string where = Transform(filter, filters);
        if (!string.IsNullOrEmpty(where) && values != null)
            queryable = queryable.Where(where, values);
        else
        {
            if (filter.Filters is not null && filter.Filters.Any())
                foreach (Filter item in filter.Filters)
                    queryable = Filter(queryable, item);
            /*if (filter.Logic == "or")
                queryable = queryable.Where("false");
            if (filter.Logic == "and")
                queryable = queryable.Where("true");*/
        }
        return queryable;
    }

    private static IQueryable<T> Sort<T>(IQueryable<T> queryable, IEnumerable<Sort> sort)
    {
        foreach (Sort item in sort)
        {
            if (string.IsNullOrEmpty(item.Field))
                throw new ArgumentException("Invalid Field");
            if (string.IsNullOrEmpty(item.Dir) || !_orders.Contains(item.Dir))
                throw new ArgumentException("Invalid Order Type");
        }

        if (sort.Any())
        {
            string ordering = string.Join(separator: ",", values: sort.Select(s => $"{s.Field} {s.Dir}"));
            return queryable.OrderBy(ordering);
        }

        return queryable;
    }

    public static IList<Filter> GetAllFilters(Filter filter)
    {
        List<Filter> filters = new();
        GetFilters(filter, filters);
        return filters;
    }

    private static void GetFilters(Filter filter, IList<Filter> filters)
    {
        filters.Add(filter);
        if (filter.Filters is not null && filter.Filters.Any())
            foreach (Filter item in filter.Filters)
                GetFilters(item, filters);
    }

    public static string Transform(Filter filter, IList<Filter> filters)
    {
        if (string.IsNullOrEmpty(filter.Field))
            return string.Empty;
        if (string.IsNullOrEmpty(filter.Operator) || !_operators.ContainsKey(filter.Operator))
            return string.Empty;

        int index = filters.IndexOf(filter);
        string comparison = _operators[filter.Operator];
        StringBuilder where = new();

        if (!string.IsNullOrEmpty(filter.Value))
        {
            // Kelimeleri ayır ve boş olanları filtrele
            var keywords = filter.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(); 

            if (keywords.Any())
            {
                StringBuilder keywordSearch = new StringBuilder();
                foreach (var keyword in keywords)
                {
                    if (keywordSearch.Length > 0)
                        keywordSearch.Append(" or ");

                    if (filter.Operator == "doesnotcontain")
                        keywordSearch.Append($"(!np({filter.Field}).ToLower().Contains(\"{keyword.ToLower()}\"))");
                    else if (comparison is "StartsWith" or "EndsWith" or "Contains")
                        keywordSearch.Append($"(np({filter.Field}).ToLower().{comparison}(\"{keyword.ToLower()}\"))");
                    else
                        keywordSearch.Append($"np({filter.Field}) {comparison} \"{keyword}\"");
                }

                where.Append($"({keywordSearch})");
            }
        }
        else if (filter.Operator is "isnull" or "isnotnull")
        {
            where.Append($"np({filter.Field}) {comparison}");
        }

        if (filter.Logic is not null && filter.Filters is not null && filter.Filters.Any())
        {
            if (!_logics.Contains(filter.Logic))
                throw new ArgumentException("Invalid Logic");
            return $"{where} {filter.Logic} ({string.Join(separator: $" {filter.Logic} ", value: filter.Filters.Select(f => Transform(f, filters)).ToArray())})";
        }

        return where.ToString();
    }




    
}
