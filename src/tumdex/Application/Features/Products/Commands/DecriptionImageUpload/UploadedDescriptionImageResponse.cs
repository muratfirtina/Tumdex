namespace Application.Features.Products.Commands.DecriptionImageUpload;

public class UploadedDescriptionImageResponse
{
    public string Url { get; set; }
    public string ImageHtml { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Alt { get; set; }
    public string? Title { get; set; }
}