using System.ComponentModel.DataAnnotations.Schema;
using Core.Persistence.Repositories;

namespace Domain;

public class ImageVersion : Entity<string>
{
    public string ImageFileId { get; set; }
    public virtual ImageFile ImageFile { get; set; }
    
    public string Size { get; set; } // thumbnail, small, medium, large
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; }
    public string Path { get; set; }
    
    [NotMapped]
    public string Url { get; set; }
    
    public string Storage { get; set; }
    public long FileSize { get; set; }
    public bool IsWebpVersion { get; set; }
    public bool IsAvifVersion { get; set; }
    
    public ImageVersion()
    {
    }

    public ImageVersion(string imageFileId, string size, int width, int height, string format)
    {
        ImageFileId = imageFileId;
        Size = size;
        Width = width;
        Height = height;
        Format = format;
    }
}
