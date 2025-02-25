namespace Application.Dtos.Image;

public class ProcessedImageVersionDto
{
    public string Size { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; }
    public long FileSize { get; set; }
    public Stream Stream { get; set; }
    public bool IsWebpVersion { get; set; }
    public bool IsAvifVersion { get; set; }
}