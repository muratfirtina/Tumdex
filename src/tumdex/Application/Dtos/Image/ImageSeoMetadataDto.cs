namespace Application.Dtos.Image;

public class ImageSeoMetadataDto
{
    public string FileName { get; set; }
    public string AltText { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? License { get; set; }
    public string? GeoLocation { get; set; }
    public string? Caption { get; set; }
}