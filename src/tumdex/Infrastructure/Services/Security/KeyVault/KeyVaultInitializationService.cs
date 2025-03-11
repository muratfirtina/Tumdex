using System.Text;
using Application.Services;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Security.KeyVault;

public class KeyVaultInitializationService : IKeyVaultInitializationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeyVaultInitializationService> _logger;
    private string? _encryptionKey;
    private string? _encryptionIV;
    private string? _encryptionSalt;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public KeyVaultInitializationService(
        IConfiguration configuration,
        ILogger<KeyVaultInitializationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_encryptionKey != null && _encryptionIV != null) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_encryptionKey != null && _encryptionIV != null) return;

            var keyVaultUrl = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI") ?? 
                              _configuration["AZURE_KEYVAULT_URI"] ??
                              _configuration["AzureKeyVault:VaultUri"];
            if (string.IsNullOrEmpty(keyVaultUrl))
            {
                throw new InvalidOperationException("KeyVault URI is not configured");
            }

            var secretClient = new SecretClient(
                new Uri(keyVaultUrl), 
                new DefaultAzureCredential());

            var keyResponse = await secretClient.GetSecretAsync("EncryptionKey");
            var ivResponse = await secretClient.GetSecretAsync("EncryptionIV");
            var saltResponse = await secretClient.GetSecretAsync("EncryptionSalt");

            // Değerleri doğrudan kullan, çift dönüştürme yapma
            _encryptionKey = keyResponse.Value.Value;
            _encryptionIV = ivResponse.Value.Value;
            _encryptionSalt = saltResponse.Value.Value;

            _logger.LogInformation("Encryption keys successfully initialized from Key Vault");
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    public string GetEncryptionSalt()
    {
        if (_encryptionSalt == null)
            throw new InvalidOperationException("Encryption salt not initialized. Call InitializeAsync first.");
        return _encryptionSalt;
    }

    public string GetEncryptionKey()
    {
        if (_encryptionKey == null)
            throw new InvalidOperationException("Encryption key not initialized. Call InitializeAsync first.");
        return _encryptionKey;
    }

    public string GetEncryptionIV()
    {
        if (_encryptionIV == null)
            throw new InvalidOperationException("Encryption IV not initialized. Call InitializeAsync first.");
        return _encryptionIV;
    }
}