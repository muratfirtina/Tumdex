using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

public class BrandImageFile : ImageFile
{
    public ICollection<Brand>Brands { get; set; }
    public string? Alt { get; set; }
    [NotMapped]
    public string Url { get; set; }

    public BrandImageFile(string? name) : base(name)
    {
        Name = name;
    }

    public BrandImageFile(string? name, string? entityType) : base(name)
    {
        Name = name;
        EntityType = entityType;
    }
    
    public BrandImageFile(string? name, string? entityType, string? path) : base(name)
    {
        Name = name;
        EntityType = entityType;
        Path = path;
    }
    
    public BrandImageFile(string? name, string? entityType, string? path, string? storage) : base(name)
    {
        Name = name;
        EntityType = entityType;
        Path = path;
        Storage = storage;
        
    }

    public BrandImageFile() : base(null)
    {
    }
}