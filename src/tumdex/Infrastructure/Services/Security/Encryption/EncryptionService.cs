using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstraction.Services;
using Application.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Security.Encryption;

public class EncryptionService : IEncryptionService
{
    private readonly ILogger<EncryptionService> _logger;
    private readonly IKeyVaultInitializationService _initializationService;
    private readonly ICacheService _cacheService;
    private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);
    private bool _isInitialized = false;
    
    public EncryptionService(
        ILogger<EncryptionService> logger, 
        IKeyVaultInitializationService initializationService,
        ICacheService cacheService)
    {
        _logger = logger;
        _initializationService = initializationService;
        _cacheService = cacheService;
    }
    
    /// <summary>
    /// Encryption anahtarlarını byte dizisine dönüştürür
    /// </summary>
    private (byte[] key, byte[] iv, byte[] salt) GetEncryptionKeys()
    {
        try 
        {
            // KeyVault'tan alınan değerleri güvenli bir şekilde dönüştür
            // Önce Key
            string keyString = _initializationService.GetEncryptionKey();
            byte[] key;
            
            // Base64 olarak çözmeyi dene
            try 
            {
                key = Convert.FromBase64String(keyString);
            }
            catch (FormatException)
            {
                // Base64 değilse, UTF8 metni olarak kabul et
                key = Encoding.UTF8.GetBytes(keyString);
                
                // AES için 32 byte (256 bit) gereklidir
                if (key.Length != 32)
                {
                    // Eğer boyut uygun değilse, hash ile boyutu düzelt
                    using var sha256 = SHA256.Create();
                    key = sha256.ComputeHash(key);
                }
            }
            
            // Sonra IV
            string ivString = _initializationService.GetEncryptionIV();
            byte[] iv;
            
            try 
            {
                iv = Convert.FromBase64String(ivString);
            }
            catch (FormatException)
            {
                iv = Encoding.UTF8.GetBytes(ivString);
                
                // AES için 16 byte (128 bit) IV gereklidir
                if (iv.Length != 16)
                {
                    // Eğer boyut uygun değilse, MD5 hash kullan (16 byte)
                    using var md5 = MD5.Create();
                    iv = md5.ComputeHash(iv);
                }
            }
            
            // Son olarak Salt
            string saltString = _initializationService.GetEncryptionSalt();
            byte[] salt;
            
            try 
            {
                salt = Convert.FromBase64String(saltString);
            }
            catch (FormatException)
            {
                salt = Encoding.UTF8.GetBytes(saltString);
                
                // Salt için 16 byte kullanacağız
                if (salt.Length != 16)
                {
                    using var md5 = MD5.Create();
                    salt = md5.ComputeHash(salt);
                }
            }
            
            return (key, iv, salt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption anahtarları dönüştürülürken hata oluştu");
            throw new InvalidOperationException("Encryption anahtarları hazırlanamadı", ex);
        }
    }

    
    /// <summary>
    /// Encryption servisinin başlatılmasını sağlar
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        // Zaten başlatılmışsa hemen dön
        if (_isInitialized)
            return;

        // Birden fazla thread aynı anda başlatma yapmasını önle
        await _initSemaphore.WaitAsync();

        try
        {
            // Tekrar kontrol et (başka bir thread başlatmış olabilir)
            if (_isInitialized)
                return;

            // KeyVault servisi başlat
            await _initializationService.InitializeAsync();
            
            // Test amaçlı olarak anahtarları dönüştürmeyi dene
            var (_, _, _) = GetEncryptionKeys();
            
            _isInitialized = true;
            _logger.LogInformation("Encryption service başarıyla başlatıldı");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption service başlatılırken hata oluştu");
            throw;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Encrypt plaintext using AES encryption algorithm
    /// </summary>
    /// <param name="plainText">Text to encrypt</param>
    /// <returns>Base64Url encoded encrypted string</returns>
    public async Task<string> EncryptAsync(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;
            
        // Başlatma işlemini garanti et
        await EnsureInitializedAsync();
        
        try
        {
            var (key, iv, _) = GetEncryptionKeys();
            byte[] encrypted;
            
            // Create an Aes object with the specified key and IV
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Padding = PaddingMode.PKCS7;
                
                // Create an encryptor to perform the stream transform
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                
                // Create the streams used for encryption
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            // Write all data to the stream
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }
            
            // Convert the encrypted bytes to a Base64Url encoded string for safe transmission
            return WebEncoders.Base64UrlEncode(encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting data");
            throw new InvalidOperationException("Failed to encrypt data", ex);
        }
    }

    /// <summary>
    /// Decrypt ciphertext that was encrypted using the Encrypt method
    /// </summary>
    /// <param name="cipherText">Base64Url encoded encrypted string</param>
    /// <returns>Decrypted plaintext</returns>
    public async Task<string> DecryptAsync(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;
            
        // Başlatma işlemini garanti et
        await EnsureInitializedAsync();
        
        try
        {
            var (key, iv, _) = GetEncryptionKeys();
            
            // Convert the Base64Url encoded string back to bytes
            byte[] cipherBytes = WebEncoders.Base64UrlDecode(cipherText);
            string plaintext = null;
            
            // Create an Aes object with the specified key and IV
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Padding = PaddingMode.PKCS7;
                
                // Create a decryptor to perform the stream transform
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                
                // Create the streams used for decryption
                using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            
            return plaintext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting data");
            throw new InvalidOperationException("Failed to decrypt data", ex);
        }
    }
    
    // Synchronous wrapper for backward compatibility
    public string Encrypt(string plainText)
    {
        return EncryptAsync(plainText).GetAwaiter().GetResult();
    }
    
    // Synchronous wrapper for backward compatibility
    public string Decrypt(string cipherText)
    {
        return DecryptAsync(cipherText).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Generate a secure token for email activation
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="email">User email</param>
    /// <returns>Encrypted token</returns>
    public async Task<string> GenerateActivationTokenAsync(string userId, string email)
    {
        // Create token data with expiration date
        var tokenData = new 
        { 
            userId, 
            email, 
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            expires = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(), // 24-hour expiration
            nonce = Guid.NewGuid().ToString() // Add a random nonce for uniqueness
        };
        
        // Serialize to JSON
        string jsonData = JsonSerializer.Serialize(tokenData);
        
        // Encrypt the JSON data
        string token = await EncryptAsync(jsonData);
        
        // Token'ı doğrulama için önbellekte sakla (tek kullanımlık olması için)
        string cacheKey = $"activation_token_{userId}";
        await _cacheService.SetAsync(cacheKey, tokenData.nonce, TimeSpan.FromHours(24));
        
        return token;
    }
    
    // Synchronous wrapper for backward compatibility
    public string GenerateActivationToken(string userId, string email)
    {
        return GenerateActivationTokenAsync(userId, email).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Verify an activation token
    /// </summary>
    public async Task<bool> VerifyActivationTokenAsync(string userId, string email, string token)
    {
        try
        {
            // Token'ı çöz
            string json = await DecryptAsync(token);
            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
            
            // Token verilerini çıkar
            var tokenUserId = tokenData.GetProperty("userId").GetString();
            var tokenEmail = tokenData.GetProperty("email").GetString();
            var expires = tokenData.GetProperty("expires").GetInt64();
            var nonce = tokenData.GetProperty("nonce").GetString();
            
            // Token verilerini doğrula
            bool isValid = tokenUserId == userId && 
                           tokenEmail == email && 
                           DateTimeOffset.FromUnixTimeSeconds(expires) > DateTimeOffset.UtcNow;
                           
            if (!isValid)
            {
                _logger.LogWarning("Token geçersiz veya süresi dolmuş: {UserId}", userId);
                return false;
            }
            
            // Önbellekteki nonce değerini kontrol ederek tekrar saldırılarını önle
            string cacheKey = $"activation_token_{userId}";
            var cachedNonce = await _cacheService.TryGetValueAsync<string>(cacheKey);
            
            if (!cachedNonce.success || cachedNonce.value != nonce)
            {
                _logger.LogWarning("Token nonce doğrulaması başarısız: {UserId}", userId);
                return false;
            }
            
            // Başarılı doğrulamadan sonra token'ı önbellekten kaldır (tek kullanımlık)
            await _cacheService.RemoveAsync(cacheKey);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token doğrulaması başarısız: {UserId}", userId);
            return false;
        }
    }
    
    /// <summary>
    /// Hash a password with optional salt
    /// </summary>
    public string HashPassword(string password, string salt = null)
    {
        // Ensure the service is initialized (synchronously)
        EnsureInitializedAsync().GetAwaiter().GetResult();
        
        // Generate salt if not provided
        if (string.IsNullOrEmpty(salt))
        {
            byte[] saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            salt = Convert.ToBase64String(saltBytes);
        }
        
        // Get encryption keys
        var (_, _, encryptionSalt) = GetEncryptionKeys();
        
        // Use PBKDF2 for secure password hashing
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            Convert.FromBase64String(salt),
            10000, // Iteration count
            HashAlgorithmName.SHA256);
            
        byte[] hash = pbkdf2.GetBytes(32); // 256-bit hash
        
        // Combine salt and hash
        return $"{salt}:{Convert.ToBase64String(hash)}";
    }
}