using System.ComponentModel.DataAnnotations.Schema;
using Core.Persistence.Repositories;

namespace Domain.Entities;

public class VideoFile : Entity<string>
{
    public string? Name { get; set; }
    public string EntityType { get; set; }
    public string Path { get; set; }
    public bool IsPrimary { get; set; } = false;
    
    [NotMapped]
    public string Url { get; set; }
    
    public string Storage { get; set; }
    
    // Video specific properties
    public string? MimeType { get; set; }
    public long Duration { get; set; } // Duration in seconds
    public string? Resolution { get; set; }
    public long FileSize { get; set; }
    
    // Metadata properties
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ThumbUrl { get; set; } // URL to the video thumbnail
    
    // For external videos
    public string? ExternalId { get; set; } // YouTube/Vimeo video ID
    public string? ExternalType { get; set; } // "youtube", "vimeo", etc.
    
    public VideoFile(string? name) : base(name)
    {
        Name = name;
    }
}