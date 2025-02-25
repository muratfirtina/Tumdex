using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Application.Storage.Yandex;
using Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage.Yandex;

public class YandexStorage : IYandexStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly IOptionsSnapshot<StorageSettings> _storageSettings;
    private readonly string _bucketName;
    private readonly string _baseUrl;

    public YandexStorage(
        IConfiguration configuration,
        IOptionsSnapshot<StorageSettings> storageSettings)
    {
        _storageSettings = storageSettings ?? throw new ArgumentNullException(nameof(storageSettings));
        
        var yandexSettings = configuration.GetSection("Storage:Providers:Yandex").Get<YandexStorageSettings>();
        if (yandexSettings == null)
        {
            throw new InvalidOperationException("Yandex Storage settings are not properly configured");
        }

        _baseUrl = yandexSettings.Url ?? throw new InvalidOperationException("Yandex Storage URL is not configured");
        _bucketName = yandexSettings.BucketName ?? throw new InvalidOperationException("Bucket name is not configured");

        var config = new AmazonS3Config
        {
            ServiceURL = "https://storage.yandexcloud.net",
            ForcePathStyle = true
        };

        var credentials = new BasicAWSCredentials(
            yandexSettings.AccessKeyId,
            yandexSettings.SecretAccessKey
        );

        _s3Client = new AmazonS3Client(credentials, config);
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
            var objectKey = $"{entityType}/{path}/{fileName}";
        
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream
            };

            await _s3Client.PutObjectAsync(putRequest);
        
            var format = Path.GetExtension(fileName).TrimStart('.').ToLower();
            var url = $"{_baseUrl}/{objectKey}";
            results.Add((fileName, path, entityType, url, format));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload file to Yandex Storage: {ex.Message}", ex);
        }

        return results;
    }

    public async Task DeleteAsync(string entityType, string path, string fileName)
    {
        try
        {
            var objectKey = $"{entityType}/{path}/{fileName}";
            
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete file from Yandex Storage: {ex.Message}", ex);
        }
    }

    public async Task<List<T>?> GetFiles<T>(string entityId, string entityType) where T : ImageFile, new()
    {
        var prefix = $"{entityType}/{entityId}/";
        var files = new List<T>();

        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest);

            foreach (var obj in listResponse.S3Objects)
            {
                files.Add(new T
                {
                    Id = Path.GetFileNameWithoutExtension(obj.Key),
                    Name = Path.GetFileName(obj.Key),
                    Path = Path.GetDirectoryName(obj.Key),
                    EntityType = entityType,
                    Storage = "yandex",
                    Url = $"{_baseUrl}/{obj.Key}"
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list files from Yandex Storage: {ex.Message}", ex);
        }

        return files;
    }

    public bool HasFile(string entityType, string path, string fileName)
    {
        try
        {
            var objectKey = $"{entityType}/{path}/{fileName}";
            var getRequest = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            var response = _s3Client.GetObjectMetadataAsync(getRequest).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetStorageUrl()
    {
        return _storageSettings.Value.Providers.Yandex?.Url ?? 
               throw new InvalidOperationException("Yandex Storage URL is not configured");
    }
}