using System.ComponentModel.DataAnnotations.Schema;
using Core.Persistence.Repositories;

namespace Domain.Entities;

public class Carousel : Entity<string>
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
    
    // New properties for video support
    public string? MediaType { get; set; } // "image" or "video"
    public string? VideoType { get; set; } // "local", "youtube", "vimeo"
    public string? VideoUrl { get; set; } // URL for external videos or path for uploaded videos
    public string? VideoId { get; set; } // For YouTube or Vimeo IDs
    
    public ICollection<CarouselImageFile> CarouselImageFiles { get; set; }
    // Optional: Add CarouselVideoFile if you want to store video metadata separately
    public CarouselVideoFile? CarouselVideoFile { get; set; }

    [NotMapped]
    public string Url { get; set; }
    
    [NotMapped]
    public bool IsVideo => MediaType == "video";
    
    public Carousel(string? name): base(name)
    {
        Name = name;
        MediaType = "image"; // Default to image type
    }
}