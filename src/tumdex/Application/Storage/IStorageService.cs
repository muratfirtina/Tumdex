using Domain;
using Microsoft.AspNetCore.Http;

namespace Application.Storage;

public interface IStorageService
{
    Task<List<(string fileName, string path, string entityType, string storageType, string url,string format)>> UploadAsync(string entityType, string path, List<IFormFile> files);
        
    Task<List<T>?> GetFiles<T>(string entityId, string entityType, string? preferredStorage = null) 
        where T : ImageFile, new();
        
    Task DeleteFromAllStoragesAsync(string entityType, string path, string fileName);
    bool HasFile(string entityType, string path, string fileName);
    string GetStorageUrl(string storageType = null);
    string GetCompanyLogoUrl();
}