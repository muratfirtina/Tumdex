using Application.Repositories;
using Application.Storage.Local;
using Core.CrossCuttingConcerns.Exceptions;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage.Local;

public class LocalStorage : ILocalStorage
{
    private readonly IImageFileRepository _imageFileRepository;
    private readonly string _baseFolderPath = Path.Combine("wwwroot");
    private readonly IOptionsSnapshot<StorageSettings> _storageSettings;
    
    public LocalStorage(IOptionsSnapshot<StorageSettings> storageSettings, IImageFileRepository imageFileRepository)
    {
        
        _imageFileRepository = imageFileRepository;
        _storageSettings = storageSettings;
        if (!Directory.Exists(_baseFolderPath))
        {
            Directory.CreateDirectory(_baseFolderPath);
        }
    }

    public async Task<List<(string fileName, string path, string containerName, string url, string format)>> UploadFileToStorage(
        string entityType, 
        string path, 
        string fileName, 
        MemoryStream fileStream)
    {
        var entityFolderPath = Path.Combine(_baseFolderPath, entityType, path);
    
        if (!Directory.Exists(entityFolderPath))
        {
            Directory.CreateDirectory(entityFolderPath);
        }
    
        var datas = new List<(string fileName, string path, string containerName, string url, string format)>();
    
        var filePath = Path.Combine(entityFolderPath, fileName);
        await using FileStream fileStream1 = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync:false);
        await fileStream.CopyToAsync(fileStream1);
        await fileStream1.FlushAsync();

        var baseUrl = _storageSettings.Value.Providers.LocalStorage.Url?.TrimEnd('/');
        var format = Path.GetExtension(fileName).TrimStart('.').ToLower();
        var url = $"{baseUrl}/{entityType}/{path}/{fileName}";
    
        datas.Add((fileName, path, entityType, url, format));

        return datas;
    }
    public async Task DeleteAsync(string entityType, string path, string fileName)
    {
        var filePath = Path.Combine(_baseFolderPath, entityType, path, fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
    

    public async Task<List<T>> GetFiles<T>(string entityId, string entityType) where T : ImageFile, new()
    {
        var baseUrl = _storageSettings.Value.Providers.LocalStorage.Url;
        var entityFolder = Path.Combine(_baseFolderPath, entityType, entityId);
        
        if (!Directory.Exists(entityFolder))
        {
            return new List<T>();
        }

        var files = Directory.GetFiles(entityFolder, "*", SearchOption.AllDirectories);
        var result = new List<T>();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(_baseFolderPath, file);
            var fileInfo = new FileInfo(file);

            result.Add(new T
            {
                Id = Path.GetFileNameWithoutExtension(file),
                Name = fileInfo.Name,
                Path = relativePath,
                EntityType = entityType,
                Storage = "localstorage",
                Url = $"{baseUrl.TrimEnd('/')}/{relativePath.Replace('\\', '/')}"
            });
        }

        return result;
    }

    public bool HasFile(string entityType, string path, string fileName) 
        => File.Exists(Path.Combine(_baseFolderPath, entityType, path, fileName));

    public string GetStorageUrl()
    {
        return _storageSettings.Value.Providers.LocalStorage.Url ?? 
               throw new InvalidOperationException("Local Storage URL is not configured");
    }

    async Task<bool> CopyFileAsync(string path, IFormFile file)
    {
        try
        {
            await using FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync:false);
            
            await file.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
            return true;

        }
        catch (Exception e)
        {
            //todo: loglama yapılacak!
            throw e;
        }

    }
    
    public async Task FileMustBeInImageFormat(IFormFile formFile)
    {
        List<string> extensions = new() { ".jpg", ".png", ".jpeg", ".webp", ".heic",".avif" };

        string extension = Path.GetExtension(formFile.FileName).ToLower();
        if (!extensions.Contains(extension))
            throw new BusinessException("Unsupported format");
        await Task.CompletedTask;
    }
    
    public async Task FileMustBeInFileFormat(IFormFile formFile)
    {
        List<string> extensions = new() { ".jpg", ".png", ".jpeg", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".heic",".avif" };

        string extension = Path.GetExtension(formFile.FileName).ToLower();
        if (!extensions.Contains(extension))
            throw new BusinessException("Unsupported format");
        await Task.CompletedTask;
    }
}