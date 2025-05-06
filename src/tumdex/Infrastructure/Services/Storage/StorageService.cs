using System.Diagnostics;
using Application.Repositories;
using Application.Services;
using Application.Storage;
using Application.Storage.Local;
using Domain;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Storage;

public class StorageService : IStorageService
{
    private readonly IStorageProviderFactory _providerFactory;
    private readonly IFileNameService _fileNameService;
    private readonly IConfiguration _configuration;
    private readonly IVideoFileRepository? _videoFileRepository;

    public StorageService(
        IStorageProviderFactory providerFactory,
        IFileNameService fileNameService,
        IConfiguration configuration, IVideoFileRepository? videoFileRepository)
    {
        _providerFactory = providerFactory;
        _fileNameService = fileNameService;
        _configuration = configuration;
        _videoFileRepository = videoFileRepository;
    }

    public async Task<List<(string fileName, string path, string entityType, string storageType, string url, string format)>> 
    UploadAsync(string entityType, string path, List<IFormFile> files)
{
    var results = new List<(string fileName, string path, string entityType, string storageType, string url, string format)>();

    foreach (var file in files)
    {
        // Dosya formatını kontrol et
        await _fileNameService.FileMustBeInFileFormat(file);
        
        // Video dosyası mı kontrol et
        bool isVideoFile = _fileNameService.IsVideoFile(file.FileName);
        string entityTypeFolder = isVideoFile ? $"{entityType}-videos" : entityType;

        // Dosya ve yol detaylarını hazırla
        var (newPath, fileNewName) = await PrepareFileDetails(file, entityTypeFolder, path);
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        // Tüm konfigüre edilmiş provider'lara yükle
        var uploadTasks = _providerFactory.GetConfiguredProviders()
            .Select(provider => UploadToProvider(provider, entityTypeFolder, newPath, fileNewName, memoryStream))
            .ToList();

        try
        {
            await Task.WhenAll(uploadTasks);

            // Aktif provider'dan URL'i al
            var activeProvider = _providerFactory.GetProvider();
            var activeProviderTask = uploadTasks
                .FirstOrDefault(t => t.Result?.Provider.GetType() == activeProvider.GetType());

            if (activeProviderTask?.Result?.Result != null)
            {
                foreach (var result in activeProviderTask.Result.Result)
                {
                    var format = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
                    var storageType = activeProvider.GetType().Name.Replace("Storage", "").ToLower();
                    results.Add((result.fileName, result.path, entityTypeFolder, storageType, result.url, format));
                }
            }

            // Eğer video dosyası ise, VideoFile tablosuna da bir kayıt ekle
            if (isVideoFile)
            {
                // MIME türünü al
                string mimeType = GetMimeType(file.FileName);
                
                // VideoFile varlığı oluştur
                var videoFile = new VideoFile(fileNewName)
                {
                    Path = newPath,
                    EntityType = entityTypeFolder,
                    Storage = activeProvider.GetType().Name.Replace("Storage", "").ToLower(),
                    MimeType = mimeType,
                    FileSize = file.Length
                };
                
                // Veritabanına ekle
                if (_videoFileRepository != null)
                {
                    await _videoFileRepository.AddAsync(videoFile);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Dosya yükleme sırasında hata: {ex.Message}");
            // Hata işleme ekleyebilirsiniz
        }
    }

    return results;
}

    public async Task<List<T>?> GetFiles<T>(
        string entityId,
        string entityType,
        string? preferredStorage = null) where T : ImageFile, new()
    {
        // Okuma işlemi için active provider'ı kullan
        var provider = _providerFactory.GetProvider(preferredStorage);
        return await provider.GetFiles<T>(entityId, entityType);
    }

    public bool HasFile(string entityType, string path, string fileName)
    {
        // Okuma işlemi için active provider'ı kullan
        var provider = _providerFactory.GetProvider();
        return provider.HasFile(entityType, path, fileName);
    }

    public string GetStorageUrl(string? storageType = null)
    {
        // Okuma işlemi için active provider'ı kullan
        var provider = _providerFactory.GetProvider(storageType);
        return provider.GetStorageUrl();
    }

    public async Task DeleteFromAllStoragesAsync(string entityType, string path, string fileName)
    {
        var providers = _providerFactory.GetConfiguredProviders();
        foreach (var provider in providers)
        {
            try
            {
                await provider.DeleteAsync(entityType, path, fileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting from {provider.GetType().Name}: {ex.Message}");
            }
        }
    }

    public string GetCompanyLogoUrl()
    {
        var storageUrl = GetStorageUrl()?.TrimEnd('/');
        var logoPath = _configuration["Storage:CompanyAssets:LogoPath"];
        if (string.IsNullOrEmpty(logoPath))
            throw new Exception("Logo path not found in configuration.");

        return $"{storageUrl}/{logoPath.TrimStart('/')}";
    }

    private async Task<(string newPath, string fileName)> PrepareFileDetails(
        IFormFile file,
        string entityType,
        string path)
    {
        await _fileNameService.FileMustBeInFileFormat(file);
        string newPath = await _fileNameService.PathRenameAsync(path);

        IFileNameService.HasFile hasFileDelegate = (pathOrContainerName, fileName) =>
            HasFile(entityType, pathOrContainerName, fileName);

        var fileNewName = await _fileNameService.FileRenameAsync(
            newPath,
            file.FileName,
            hasFileDelegate);

        return (newPath, fileNewName);
    }

    private class UploadTask
    {
        public IStorageProvider? Provider { get; init; }

        public List<(string fileName, string path, string containerName, string url, string format)>? Result
        {
            get;
            set;
        }
    }

    private async Task<UploadTask> UploadToProvider(
        IStorageProvider provider,
        string entityType,
        string path,
        string fileName,
        MemoryStream memoryStream)
    {
        var uploadTask = new UploadTask { Provider = provider };

        try
        {
            memoryStream.Position = 0;
            var result = await provider.UploadFileToStorage(
                entityType,
                path,
                fileName,
                new MemoryStream(memoryStream.ToArray())
            );
            uploadTask.Result = result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error uploading to {provider.GetType().Name}: {ex.Message}");
        }

        return uploadTask;
    }
    
    private string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        return extension switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            _ => "application/octet-stream" // Default type if unknown
        };
    }
}