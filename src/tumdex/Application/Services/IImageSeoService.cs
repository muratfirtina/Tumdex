using Application.Dtos.Image;

namespace Application.Services;

public interface IImageSeoService
{
    Task<ImageProcessingResultDto> ProcessAndOptimizeImage(
        Stream imageStream, 
        string fileName, 
        ImageProcessingOptionsDto options);
        
    Task<bool> ValidateImage(Stream imageStream, string fileName);
    Task<string> GenerateImageSitemap();
}