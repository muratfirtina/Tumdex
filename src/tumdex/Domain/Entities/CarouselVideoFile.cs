namespace Domain.Entities;

public class CarouselVideoFile : VideoFile
{
    ICollection<Carousel> Carousels { get; set; }
    
    public CarouselVideoFile(string? name) : base(name)
    {
        Name = name;
    }
    
    public CarouselVideoFile(string? name, string? entityType, string? path, string? storage) : base(name)
    {
        Name = name;
        EntityType = entityType;
        Path = path;
        Storage = storage;
    }
    
    public CarouselVideoFile() : base(null)
    {
    }
}