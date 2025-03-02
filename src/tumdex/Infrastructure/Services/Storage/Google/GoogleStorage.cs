using Application.Storage.Google;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Domain;
using Domain.Entities;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage.Google;

public class GoogleStorage : IGoogleStorage
{
    private readonly StorageClient _storageClient;
    private readonly IOptionsSnapshot<StorageSettings> _storageSettings;
    private readonly SecretClient _secretClient;
    private readonly string _baseUrl;
    private readonly string _bucketName;
    private readonly IConfiguration _configuration;

    public GoogleStorage(
        IConfiguration configuration, 
        IOptionsSnapshot<StorageSettings> storageSettings, SecretClient secretClient)
    {
        _configuration = configuration;
        _storageSettings = storageSettings ?? throw new ArgumentNullException(nameof(storageSettings));
        _secretClient = secretClient;

        var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI") ?? 
                          throw new InvalidOperationException("AZURE_KEYVAULT_URI not found");
            
        var credential = new DefaultAzureCredential();
        _secretClient = new SecretClient(new Uri(keyVaultUri), credential);

        _bucketName = _secretClient.GetSecret("GoogleStorageBucketName").Value.Value;
        _baseUrl = _secretClient.GetSecret("GoogleStorageUrl").Value.Value;
        var credentialsPath = _secretClient.GetSecret("GoogleStorageCredentialsPath").Value.Value;

        var googleCredential = GoogleCredential.FromFile(credentialsPath);
        _storageClient = StorageClient.Create(googleCredential);
    }
    

    public async Task<List<(string fileName, string path, string containerName, string url, string format)>> UploadFileToStorage(
        string entityType, 
        string path, 
        string fileName, 
        MemoryStream fileStream)
    {
        var results = new List<(string fileName, string path, string containerName, string url, string format)>();
        try
        {
            var objectName = $"{entityType}/{path}/{fileName}";
            await _storageClient.UploadObjectAsync(_bucketName, objectName, null, fileStream);
        
            var format = Path.GetExtension(fileName).TrimStart('.').ToLower();
            
            // Düzeltilmiş URL formatı
            var url = $"https://storage.googleapis.com/{_bucketName}/{objectName}";
            results.Add((fileName, path, entityType, url, format));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload file to Google Storage: {ex.Message}", ex);
        }

        return results;
    }

    public async Task DeleteAsync(string entityType, string path, string fileName)
    {
        try
        {
            var objectName = $"{entityType}/{path}/{fileName}";
            await _storageClient.DeleteObjectAsync(_bucketName, objectName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete file from Google Storage: {ex.Message}", ex);
        }
    }

    public async Task<List<T>?> GetFiles<T>(string entityId, string entityType) where T : ImageFile, new()
    {
        try
        {
            var prefix = $"{entityType}/{entityId}/";
            var objects = _storageClient.ListObjects(_bucketName, prefix);

            return objects.Select(obj => new T
            {
                Id = Path.GetFileNameWithoutExtension(obj.Name),
                Name = Path.GetFileName(obj.Name),
                Path = Path.GetDirectoryName(obj.Name),
                EntityType = entityType,
                Storage = "Google",
                Url = $"{_baseUrl}/{obj.Name}"
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get files from Google Storage: {ex.Message}", ex);
        }
    }

    public bool HasFile(string entityType, string path, string fileName)
    {
        try
        {
            var objectName = $"{entityType}/{path}/{fileName}";
            var obj = _storageClient.GetObject(_bucketName, objectName, new GetObjectOptions 
            { 
                Projection = Projection.NoAcl 
            });
            return obj != null;
        }
        catch
        {
            return false;
        }
    }

    public string GetStorageUrl()
    {
        return $"https://storage.googleapis.com/{_bucketName}";
    }
}
