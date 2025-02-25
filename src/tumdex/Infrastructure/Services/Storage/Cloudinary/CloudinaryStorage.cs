using Application.Storage.Cloudinary;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Domain;
using Infrastructure.Adapters.Image.Cloudinary;
using Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage.Cloudinary;

public class CloudinaryStorage : ICloudinaryStorage
{
    private readonly CloudinaryDotNet.Cloudinary _cloudinary;
    private readonly IOptionsSnapshot<StorageSettings> _storageSettings;
    private readonly IConfiguration _configuration;
    private readonly SecretClient _secretClient;
    
    public CloudinaryStorage(
        IOptionsSnapshot<StorageSettings> storageSettings,
        IConfiguration configuration, SecretClient secretClient)
    {
        _storageSettings = storageSettings;
        _configuration = configuration;
        _secretClient = secretClient;

        // Azure Key Vault'tan Cloudinary ayarlarını al
        var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI") ?? 
                          throw new InvalidOperationException("AZURE_KEYVAULT_URI not found");
            
        var credential = new DefaultAzureCredential();
        _secretClient = new SecretClient(new Uri(keyVaultUri), credential);

        var cloudName = _secretClient.GetSecret("CloudinaryCloudName").Value.Value;
        var apiKey = _secretClient.GetSecret("CloudinaryApiKey").Value.Value;
        var apiSecret = _secretClient.GetSecret("CloudinaryApiSecret").Value.Value;

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new CloudinaryDotNet.Cloudinary(account);
    }
    
    
    public async Task<List<(string fileName, string path, string containerName, string url, string format)>> UploadFileToStorage(
        string entityType, 
        string path, 
        string fileName, 
        MemoryStream fileStream)
    {
        try 
        {
            var datas = new List<(string fileName, string path, string containerName, string url, string format)>();
        
            ImageUploadParams imageUploadParams = new()
            {
                File = new FileDescription(fileName, stream: fileStream),
                UseFilename = true,
                UniqueFilename = false,
                Overwrite = false,
                Folder = $"{entityType}/{path}",
                PublicId = Path.GetFileNameWithoutExtension(fileName)
            };
        
            var uploadResult = await _cloudinary.UploadAsync(imageUploadParams);
        
            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }
            
            var format = Path.GetExtension(fileName).TrimStart('.').ToLower();
            datas.Add((fileName, path, entityType, uploadResult.SecureUrl.ToString(), format));
        
            return datas;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload file {fileName}", ex);
        }
    }

    public async Task DeleteAsync(string entityType, string path, string fileName)
    {
        try 
        {
            var publicId = GetPublicId($"{entityType}/{path}/{fileName}");
            if (!string.IsNullOrEmpty(publicId))
            {
                var deletionParams = new DeletionParams(publicId)
                {
                    ResourceType = ResourceType.Image
                };
                var result = await _cloudinary.DestroyAsync(deletionParams);
                
                if (result.Error != null)
                {
                    throw new InvalidOperationException($"Cloudinary deletion failed: {result.Error.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete file {fileName}", ex);
        }
    }

    public async Task<List<T>?> GetFiles<T>(string entityId, string entityType) where T : ImageFile, new()
    {
        try
        {
            var baseUrl = _storageSettings.Value.Providers.Cloudinary.Url;
            var searchExpression = $"folder:{entityType}/{entityId}";
            var searchResult = _cloudinary.Search()
                .Expression(searchExpression)
                .Execute();

            return searchResult.Resources.Select(resource => new T
            {
                Id = Path.GetFileNameWithoutExtension(resource.PublicId),
                Name = Path.GetFileName(resource.PublicId),
                Path = Path.GetDirectoryName(resource.PublicId),
                EntityType = entityType,
                Storage = "Cloudinary",
                Url = resource.SecureUrl.ToString()
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get files for {entityType}/{entityId}", ex);
        }
    }

    public bool HasFile(string entityType, string path, string fileName)
    {
        try
        {
            var publicId = GetPublicId($"{entityType}/{path}/{fileName}");
            var getResourceParams = new GetResourceParams(publicId)
            {
                ResourceType = ResourceType.Image
            };

            var result = _cloudinary.GetResource(getResourceParams);
            return result != null && !string.IsNullOrEmpty(result.PublicId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string GetStorageUrl()
    {
        return _storageSettings.Value.Providers.Cloudinary.Url ?? 
               throw new InvalidOperationException("Cloudinary URL is not configured");
    }

    private string GetPublicId(string fullPath)
    {
        var pathParts = fullPath.Split('/');
        if (pathParts.Length >= 3)
        {
            var fileName = pathParts[pathParts.Length - 1];
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            
            return string.Join("/", pathParts.Take(pathParts.Length - 1).Append(fileNameWithoutExtension));
        }
        return string.Empty;
    }
}