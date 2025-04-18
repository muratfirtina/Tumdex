using Domain;
using Domain.Entities;

namespace Application.Storage;

public interface IBlobService
{
    Task<List<(string fileName, string path, string containerName)>> UploadFileToStorage(string entityType, string path, string fileName, MemoryStream fileStream);
    Task DeleteAsync(string entityType, string path, string fileName);
    Task<List<T>?> GetFiles<T>(string entityId, string entityType) where T : ImageFile, new();
    bool HasFile(string entityType, string path, string fileName);
}