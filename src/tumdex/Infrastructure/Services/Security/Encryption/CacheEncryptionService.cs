using System.Security.Cryptography;
using Application.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Security.Encryption;

public class CacheEncryptionService : ICacheEncryptionService
{
    private readonly IKeyVaultInitializationService _initializationService;
    private readonly ILogger<CacheEncryptionService> _logger;

    public CacheEncryptionService(
        IKeyVaultInitializationService initializationService,
        ILogger<CacheEncryptionService> logger)
    {
        _initializationService = initializationService;
        _logger = logger;
    }

    public async Task<string> EncryptForCache(string plainText)
    {
        try
        {
            await _initializationService.InitializeAsync();
            
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(_initializationService.GetEncryptionKey());
            aes.IV = Convert.FromBase64String(_initializationService.GetEncryptionIV());

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using var msEncrypt = new MemoryStream();
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                await swEncrypt.WriteAsync(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed for cache value");
            throw new ApplicationException("Cache encryption failed", ex);
        }
    }

    public async Task<string> DecryptFromCache(string cipherText)
    {
        try
        {
            await _initializationService.InitializeAsync();

            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(_initializationService.GetEncryptionKey());
            aes.IV = Convert.FromBase64String(_initializationService.GetEncryptionIV());

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            
            return await srDecrypt.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed for cache value");
            throw new ApplicationException("Cache decryption failed", ex);
        }
    }
}