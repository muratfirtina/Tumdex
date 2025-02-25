using Domain;

namespace Application.Storage;

public interface IStorageProvider
{
    Task<List<(string fileName, string path, string containerName, string url, string format)>> UploadFileToStorage(
        string entityType, 
        string path, 
        string fileName, 
        MemoryStream fileStream);

    Task DeleteAsync(string entityType, string path, string fileName);
    Task<List<T>?> GetFiles<T>(string entityId, string entityType) where T : ImageFile, new();
    bool HasFile(string entityType, string path, string fileName);
    string GetStorageUrl();
}

