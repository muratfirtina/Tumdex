using Application.Dtos.Assets;
using Application.Storage;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services.Storage;

public class CompanyAssetService : ICompanyAssetService
{
    private readonly IStorageService _storageService;
    private const string ENTITY_TYPE = "company";
    private const string LOGO_PATH = "logos";
    private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".svg" };

    public CompanyAssetService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<bool> UploadLogoAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
            throw new ArgumentException("Invalid file type");

        if (file.Length > 2 * 1024 * 1024) // 2MB limit
            throw new ArgumentException("File size exceeds limit");

        var files = new List<IFormFile> { file };
        
        try
        {
            var results = await _storageService.UploadAsync(
                entityType: ENTITY_TYPE,
                path: LOGO_PATH,
                files: new List<IFormFile> { file }
            );

            if (results.Any())
            {
                var uploadedFile = results.First();
                // URL'i Cloudinary'nin beklediği formatta güncelle
                var newUrl = $"{_storageService.GetStorageUrl()}//{uploadedFile.entityType}/{uploadedFile.path}/{uploadedFile.fileName}";
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<CompanyLogoDto> GetLogoAsync()
    {
        return new CompanyLogoDto
        {
            Url = _storageService.GetCompanyLogoUrl(),
        };
    }

    public async Task<bool> UpdateLogoAsync(IFormFile file)
    {
        try
        {
            // Önce eski logoları sil
            foreach (var ext in _allowedExtensions)
            {
                var fileName = $"logo{ext}";
                await _storageService.DeleteFromAllStoragesAsync(ENTITY_TYPE, LOGO_PATH, fileName);
            }

            // Yeni logoyu yükle
            return await UploadLogoAsync(file);
        }
        catch (Exception ex)
        {
            // Log error
            return false;
        }
    }
}