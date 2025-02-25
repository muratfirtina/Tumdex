using System.Xml.Linq;
using Application.Services;
using Application.Storage;
using Application.Dtos.Image;
using Application.Repositories;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using SkiaSharp;
using System.IO;
using Newtonsoft.Json;

namespace Infrastructure.Services.Seo;

public class ImageSeoService : IImageSeoService
{
    private readonly IStorageService _storageService;
    private readonly IFileNameService _fileNameService;
    private readonly IConfiguration _configuration;
    private readonly IImageFileRepository _imageFileRepository;
    private const int MaxFileSizeMB = 5;
    private readonly string[] _supportedFormats = { ".jpg", ".jpeg", ".png", ".webp", ".avif", ".heic" };

    public ImageSeoService(
        IStorageService storageService,
        IFileNameService fileNameService,
        IConfiguration configuration,
        IImageFileRepository imageFileRepository)
    {
        _storageService = storageService;
        _fileNameService = fileNameService;
        _configuration = configuration;
        _imageFileRepository = imageFileRepository;
    }
    

    public async Task<ImageProcessingResultDto> ProcessAndOptimizeImage(
        Stream imageStream,
        string fileName,
        ImageProcessingOptionsDto options)
    {
        await ValidateImage(imageStream, fileName);

        string sanitizedFileName = await _fileNameService.FileRenameAsync(
            options.Path ?? "images",
            fileName,
            (path, name) => _storageService.HasFile(options.EntityType ?? "general", path, name)
        );

        var result = new ImageProcessingResultDto
        {
            OriginalFileName = sanitizedFileName
        };

        var sizes = new Dictionary<string, (int width, int height)>
        {
            { "thumbnail", (150, 150) },
            { "small", (480, 0) },
            { "medium", (768, 0) },
            { "large", (1200, 0) }
        };

        using (var originalBitmap = SKBitmap.Decode(imageStream))
        {
            foreach (var (size, dimensions) in sizes)
            {
                await ProcessImageVersions(
                    originalBitmap,
                    sanitizedFileName,
                    size,
                    dimensions,
                    result,
                    options.EntityType ?? "general",
                    options.Path ?? "images"
                );
            }
        }

        result.SeoMetadata = new ImageSeoMetadataDto
        {
            FileName = Path.GetFileNameWithoutExtension(sanitizedFileName),
            AltText = options.AltText ?? Path.GetFileNameWithoutExtension(fileName),
            Title = options.Title,
            Description = options.Description,
            License = options.License,
            GeoLocation = options.GeoLocation,
              Caption = options.Caption
        };

        return result;
    }

    private async Task ProcessImageVersions(
        SKBitmap originalBitmap,
        string fileName,
        string size,
        (int width, int height) dimensions,
        ImageProcessingResultDto result,
        string entityType,
        string path)
    {
        var height = dimensions.height == 0
            ? (int)((float)dimensions.width / originalBitmap.Width * originalBitmap.Height)
            : dimensions.height;
        
        

        using (var resizedBitmap = originalBitmap.Resize(
            new SKImageInfo(dimensions.width, height),
            SKFilterQuality.High))
        {
            // AVIF versiyonu
            await CreateOptimizedVersion(resizedBitmap, fileName, size, "avif", result, true);

            // WebP versiyonu
            await CreateOptimizedVersion(resizedBitmap, fileName, size, "webp", result);

            // Orijinal format versiyonu
            string originalFormat = Path.GetExtension(fileName).TrimStart('.').ToLower();
            await CreateOptimizedVersion(resizedBitmap, fileName, size, originalFormat, result);

            // Tüm versiyonları storage'a yükle
            foreach (var version in result.Versions.TakeLast(3))
            {
                await UploadVersionToStorage(version, entityType, path, fileName, size, version.Format);
            }
        }
    }

    private async Task CreateOptimizedVersion(
        SKBitmap bitmap,
        string fileName,
        string size,
        string format,
        ImageProcessingResultDto result,
        bool isAvif = false)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var stream = new MemoryStream();

        SKEncodedImageFormat encodedFormat = GetSkiaFormat(format);
        int quality = GetQualityForFormat(format);

        var data = image.Encode(encodedFormat, quality);
        await using (var imageStream = data.AsStream())
        {
            await imageStream.CopyToAsync(stream);
        }

        result.Versions.Add(new ProcessedImageVersionDto
        {
            Size = size,
            Width = bitmap.Width,
            Height = bitmap.Height,
            Format = format,
            Stream = stream,
            FileSize = stream.Length,
            IsAvifVersion = isAvif,
            IsWebpVersion = format == "webp"
        });
    }

    private SKEncodedImageFormat GetSkiaFormat(string format) =>
        format.ToLower() switch
        {
            "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
            "png" => SKEncodedImageFormat.Png,
            "webp" => SKEncodedImageFormat.Webp,
            "avif" or "heif" or "heic" => SKEncodedImageFormat.Heif,
            _ => SKEncodedImageFormat.Jpeg
        };

    private int GetQualityForFormat(string format) =>
        format.ToLower() switch
        {
            "jpg" or "jpeg" => 85,
            "png" => 90,
            "webp" => 80,
            "avif" or "heif" or "heic" => 75,
            _ => 85
        };

    private async Task UploadVersionToStorage(
        ProcessedImageVersionDto version,
        string entityType,
        string path,
        string fileName,
        string size,
        string format)
    {
        var versionFileName = $"{Path.GetFileNameWithoutExtension(fileName)}-{size}.{format}";

        version.Stream.Position = 0;
        var formFile = new FormFile(
            version.Stream,
            0,
            version.Stream.Length,
            "image",
            versionFileName);

        await _storageService.UploadAsync(
            entityType,
            path,
            new List<IFormFile> { formFile });
    }

    public async Task<bool> ValidateImage(Stream imageStream, string fileName)
    {
        var format = Path.GetExtension(fileName).TrimStart('.').ToLower();
        if (!_supportedFormats.Contains(format))
        {
            throw new BusinessException($"Desteklenmeyen görsel formatı. Desteklenen formatlar: {string.Join(", ", _supportedFormats)}");
        }
        try
        {
            await _fileNameService.FileMustBeInFileFormat(new FormFile(
                imageStream,
                0,
                imageStream.Length,
                "image",
                fileName));

            if (imageStream.Length > MaxFileSizeMB * 1024 * 1024)
            {
                throw new BusinessException($"Görsel boyutu {MaxFileSizeMB}MB'dan küçük olmalıdır.");
            }

            try
            {
                using var codec = SKCodec.Create(imageStream);
                if (codec == null)
                {
                    throw new BusinessException("Görsel dosyası bozuk veya okunamıyor.");
                }

                var info = codec.Info;
                if (info.Width < 100 || info.Height < 100)
                {
                    throw new BusinessException("Görsel boyutları çok küçük. Minimum 100x100 piksel olmalıdır.");
                }

                if (info.Width > 8000 || info.Height > 8000)
                {
                    throw new BusinessException("Görsel boyutları çok büyük. Maksimum 8000x8000 piksel olmalıdır.");
                }

                imageStream.Position = 0;
                return true;
            }
            catch (Exception ex) when (ex is not BusinessException)
            {
                throw new BusinessException("Görsel dosyası bozuk veya okunamıyor.");
            }
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"Görsel doğrulama sırasında beklenmeyen hata: {ex.Message}");
        }
    }

      private string GenerateProductJsonLd(ImageFile image)
        {
            var baseUrl = _configuration["WebAPIConfiguration:APIDomain:0"];
            var jsonLd = new
            {
                context = "https://schema.org",
                type = "ImageObject",
                url = image.Url,
                name = image.Name,
                description = image.Description,
                 uploadDate = image.CreatedDate.ToString("yyyy-MM-dd"),
                license = image.License,
                caption= image.Caption,
                
                 encodingFormat = image.Format,
                 width=image.Width,
                 height = image.Height
            };
            return JsonConvert.SerializeObject(jsonLd, Formatting.Indented);
        }
        public async Task<string> GenerateImageSitemap()
        {
            var baseUrl = _configuration["WebAPIConfiguration:APIDomain:0"];
            var xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var xmlnsImage = "http://www.google.com/schemas/sitemap-image/1.1";

            var sitemap = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(XName.Get("urlset", xmlns),
                    new XAttribute(XNamespace.Xmlns + "image", xmlnsImage)));

            var images = await _imageFileRepository.GetAllAsync();
            var imageNamespace = XNamespace.Get(xmlnsImage);

            foreach (var image in images)
            {
                var url = new XElement("url",
                    new XElement("loc", $"{baseUrl}/images/{image.Id}"),
                    new XElement(imageNamespace + "image",
                        new XElement(imageNamespace + "loc", image.Url),
                        new XElement(imageNamespace + "title", image.Title ?? image.Name),
                        new XElement(imageNamespace + "caption", image.Caption ?? image.Description),
                        image.GeoLocation != null ? new XElement(imageNamespace + "geo_location", image.GeoLocation) : null,
                        image.License != null ? new XElement(imageNamespace + "license", image.License) : null
                    ),
                    new XElement("lastmod", image.UpdatedDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd"))
                );
                sitemap.Root?.Add(url);
            }

            return sitemap.ToString();
        }
}