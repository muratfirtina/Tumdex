using System.ComponentModel.DataAnnotations.Schema;

namespace Domain;

public class ProductImageFile:ImageFile
{
    public bool Showcase { get; set; } = false;
    public string? Alt { get; set; }
    public ICollection<Product> Products { get; set; }
    [NotMapped]
    public string Url { get; set; }
    

    public ProductImageFile(string? name) : base(name)
    {
        Name = name;
    }

    public ProductImageFile(string? name, string? entityType) : base(name)
    {
        Name = name;
        EntityType = entityType;
    }
    
    public ProductImageFile(string? name, string? entityType, string? path) : base(name)
    {
        Name = name;
        EntityType = entityType;
        Path = path;
    }
    
    public ProductImageFile(string? name, string? entityType, string? path, string? storage) : base(name)
    {
        Name = name;
        EntityType = entityType;
        Path = path;
        Storage = storage;
        
    }

    public ProductImageFile() : base(null)
    {
    }
    
    
}