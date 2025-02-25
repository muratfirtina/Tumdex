namespace Application.Services;

public interface ICacheEncryptionService
{
    Task<string> EncryptForCache(string plainText);
    Task<string> DecryptFromCache(string cipherText);
}