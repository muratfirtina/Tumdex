using Application.Abstraction.Services;
using Application.Abstraction.Services.Configurations;
using Application.Abstraction.Services.Utilities;
using Application.Services;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Security.KeyVault;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient? _secretClient;
    private readonly ICacheService _cache;
    private readonly ILogger<KeyVaultService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ICacheEncryptionService _cacheEncryption;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private const string CacheKeyPrefix = "KeyVault_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "JwtSecurityKey",
        "JwtIssuer",
        "JwtAudience",
        "DatabasePassword",
        "RedisPassword",
        "SmtpPassword"
    };

    public KeyVaultService(
        IConfiguration configuration,
        ICacheService cache,
        ILogger<KeyVaultService> logger,
        ICacheEncryptionService cacheEncryption)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheEncryption = cacheEncryption ?? throw new ArgumentNullException(nameof(cacheEncryption));

        if (_configuration.GetValue<bool>("UseAzureKeyVault", true))
        {
            var keyVaultUrl = GetKeyVaultUri();
            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                var credential = CreateAzureCredential();
                _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
            }
        }
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        ArgumentNullException.ThrowIfNull(secretName);
        
        var cacheKey = $"{CacheKeyPrefix}{secretName}";
        
        try
        {
            // Redis'ten kontrol et
            var (success, cachedValue) = await _cache.TryGetValueAsync<string>(cacheKey,cancellationToken: CancellationToken.None);
            if (success)
            {
                _logger.LogDebug("Cache hit for secret: {SecretName}", secretName);
                return await DecryptIfNeededAsync(secretName, cachedValue);
            }

            await _semaphore.WaitAsync();
            try
            {
                // Double check
                (success, cachedValue) = await _cache.TryGetValueAsync<string>(cacheKey,cancellationToken: CancellationToken.None);
                if (success)
                {
                    return await DecryptIfNeededAsync(secretName, cachedValue);
                }

                var value = await GetSecretFromSourceAsync(secretName);
                await CacheSecretAsync(secretName, value);
                
                return value;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret: {SecretName}", secretName);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetSecretsAsync(string[] secretNames)
    {
        ArgumentNullException.ThrowIfNull(secretNames);

        var tasks = secretNames.Select(async name =>
        {
            try
            {
                var value = await GetSecretAsync(name);
                return new KeyValuePair<string, string>(name, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret: {SecretName}", name);
                return new KeyValuePair<string, string>(name, string.Empty);
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public async Task SetSecretAsync(string secretName, string value, bool recoverIfDeleted = false)
    {
        ArgumentNullException.ThrowIfNull(secretName);
        ArgumentNullException.ThrowIfNull(value);

        await _semaphore.WaitAsync();
        try
        {
            if (_secretClient != null)
            {
                if (recoverIfDeleted)
                {
                    await RecoverDeletedSecretIfExistsAsync(secretName);
                }

                await _secretClient.SetSecretAsync(secretName, value);
                _logger.LogInformation("Secret set successfully: {SecretName}", secretName);
            }

            await InvalidateCacheAsync(secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret: {SecretName}", secretName);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetSecretsAsync(Dictionary<string, string> secrets, bool recoverIfDeleted = false)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        var tasks = secrets.Select(secret => 
            SetSecretAsync(secret.Key, secret.Value, recoverIfDeleted));
            
        await Task.WhenAll(tasks);
    }

    public async Task<Dictionary<string, string>> GetAllSecretsAsync()
    {
        var secrets = new Dictionary<string, string>();
        
        try
        {
            if (_secretClient != null)
            {
                await foreach (var secretProperty in _secretClient.GetPropertiesOfSecretsAsync())
                {
                    try
                    {
                        var secret = await _secretClient.GetSecretAsync(secretProperty.Name);
                        secrets.Add(secretProperty.Name, secret.Value.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to retrieve secret: {SecretName}", secretProperty.Name);
                    }
                }
            }
            else
            {
                var configs = _configuration.AsEnumerable()
                    .Where(x => !string.IsNullOrEmpty(x.Value));
                    
                foreach (var config in configs)
                {
                    secrets.Add(config.Key, config.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve all secrets");
            throw;
        }

        return secrets;
    }

    public async Task DeleteSecretAsync(string secretName)
    {
        ArgumentNullException.ThrowIfNull(secretName);

        await _semaphore.WaitAsync();
        try
        {
            if (_secretClient != null)
            {
                await _secretClient.StartDeleteSecretAsync(secretName);
                _logger.LogInformation("Secret deleted successfully: {SecretName}", secretName);
            }

            await InvalidateCacheAsync(secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret: {SecretName}", secretName);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Dictionary<string, bool>> DeleteSecretsAsync(string[] secretNames)
    {
        ArgumentNullException.ThrowIfNull(secretNames);

        var results = new Dictionary<string, bool>();
        var tasks = secretNames.Select(async secretName =>
        {
            try
            {
                await DeleteSecretAsync(secretName);
                results[secretName] = true;
            }
            catch (Exception)
            {
                results[secretName] = false;
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    private string GetKeyVaultUri()
    {
        return Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI") ?? 
               _configuration["AZURE_KEYVAULT_URI"] ??
               _configuration["AzureKeyVault:VaultUri"];
    }

    private static DefaultAzureCredential CreateAzureCredential()
    {
        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            Retry =
            {
                MaxRetries = 3,
                NetworkTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    private async Task<string> GetSecretFromSourceAsync(string secretName)
    {
        if (_secretClient != null)
        {
            try
            {
                var response = await _secretClient.GetSecretAsync(secretName);
                return response.Value.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Secret not found in Key Vault: {SecretName}", secretName);
            }
        }

        var configValue = _configuration[secretName];
        if (string.IsNullOrEmpty(configValue))
        {
            _logger.LogWarning("Secret not found in configuration: {SecretName}", secretName);
            return string.Empty;
        }

        return configValue;
    }

    private async Task CacheSecretAsync(string secretName, string value)
    {
        var cacheKey = $"{CacheKeyPrefix}{secretName}";
        var valueToCache = value;

        if (SensitiveKeys.Contains(secretName))
        {
            valueToCache = await _cacheEncryption.EncryptForCache(value);
        }

        await _cache.SetAsync(cacheKey, valueToCache, CacheDuration,cancellationToken: CancellationToken.None);
    }

    private async Task InvalidateCacheAsync(string secretName)
    {
        var cacheKey = $"{CacheKeyPrefix}{secretName}";
        await _cache.RemoveAsync(cacheKey,cancellationToken: CancellationToken.None);
    }

    private async Task<string> DecryptIfNeededAsync(string secretName, string value)
    {
        return SensitiveKeys.Contains(secretName) 
            ? await _cacheEncryption.DecryptFromCache(value) 
            : value;
    }

    private async Task RecoverDeletedSecretIfExistsAsync(string secretName)
    {
        try
        {
            var deletedSecret = await _secretClient.GetDeletedSecretAsync(secretName);
            if (deletedSecret.Value != null)
            {
                _logger.LogInformation("Recovering deleted secret: {SecretName}", secretName);
                var recoverOperation = await _secretClient.StartRecoverDeletedSecretAsync(secretName);
                await recoverOperation.WaitForCompletionAsync();
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("No deleted secret found to recover: {SecretName}", secretName);
        }
    }
}