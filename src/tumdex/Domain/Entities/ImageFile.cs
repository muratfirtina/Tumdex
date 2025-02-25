using System.ComponentModel.DataAnnotations.Schema;
using Core.Persistence.Repositories;

namespace Domain;

public class ImageFile : Entity<string>
{
    public string? Name { get; set; }
    public string EntityType { get; set; }
    public string Path { get; set; }
    
    [NotMapped]
    public string Url { get; set; }
    
    public string Storage { get; set; }
    
    // SEO Properties
    public string? Title { get; set; }
    public string? Alt { get; set; }
    public string? Description { get; set; }
    public string? License { get; set; }
    public string? GeoLocation { get; set; }
    public string? Caption { get; set; }
    
    // Image Properties
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; }
    public long FileSize { get; set; }
    
    // Navigation Properties
    public virtual ICollection<ImageVersion> Versions { get; set; }
    
    public ImageFile(string? name) : base(name)
    {
        Name = name;
        Versions = new List<ImageVersion>();
    }
}