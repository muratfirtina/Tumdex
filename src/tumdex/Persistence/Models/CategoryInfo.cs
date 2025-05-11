namespace Persistence.Models;

public class CategoryInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ParentId { get; set; }
    public int ProductCount { get; set; }
}