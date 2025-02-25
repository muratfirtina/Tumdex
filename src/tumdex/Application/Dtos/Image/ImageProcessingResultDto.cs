namespace Application.Dtos.Image;

public class ImageProcessingResultDto
{
    public string OriginalFileName { get; set; }
    public List<ProcessedImageVersionDto> Versions { get; set; } = new();
    public ImageSeoMetadataDto SeoMetadata { get; set; }
}