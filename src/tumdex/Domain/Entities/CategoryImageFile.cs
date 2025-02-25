using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

public class CategoryImageFile : ImageFile
{
    public ICollection<Category>Categories { get; set; }
    public string? Alt { get; set; }
    [NotMapped]
    public string Url { get; set; }

    public CategoryImageFile(string? name) : base(name)
    {
        Name = name;
    }

    public CategoryImageFile(string? name, string? entityType) : base(name)
    {
        Name = name;
        EntityType = entityType;
    }
    
    public CategoryImageFile(string? name, string? entityType, string? path) : base(name)
    {
        Name = name;
        EntityType = entityType;
        Path = path;
    }
    
    public CategoryImageFile(string? name, string? entityType, string? path, string? storage) : base(name)
    {
        Name = name;
        EntityType = entityType;
        Path = path;
        Storage = storage;
        
    }

    public CategoryImageFile() : base(null)
    {
    }
}