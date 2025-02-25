namespace Domain;

public class CarouselImageFile : ImageFile
{
    ICollection<Carousel> Carousels { get; set; }
    
    public CarouselImageFile(string? name) : base(name)
    {
        Name = name;
    }
    
    public CarouselImageFile(string? name, string? entityType, string? path, string? storage) : base(name)
    {
        Name = name;
        EntityType = entityType;
        Path = path;
        Storage = storage;
        
    }
    
    public CarouselImageFile() : base(null)
    {
    }
}