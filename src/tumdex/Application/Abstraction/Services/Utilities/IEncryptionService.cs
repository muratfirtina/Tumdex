namespace Application.Abstraction.Services.Utilities;

/// <summary>
/// Veri şifreleme, çözme ve güvenli token yönetimi işlemleri için arayüz
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Metni AES algoritması kullanarak şifreler
    /// </summary>
    /// <param name="plainText">Şifrelenecek düz metin</param>
    /// <returns>Base64 kodlanmış şifreli metin</returns>
    Task<string> EncryptAsync(string plainText);
    
    /// <summary>
    /// Şifreli metni çözer
    /// </summary>
    /// <param name="cipherText">Base64 kodlanmış şifreli metin</param>
    /// <returns>Çözülmüş düz metin</returns>
    Task<string> DecryptAsync(string cipherText);
    
    /// <summary>
    /// Geriye dönük uyumluluk için senkron şifreleme metodu
    /// </summary>
    /// <param name="plainText">Şifrelenecek düz metin</param>
    /// <returns>Base64 kodlanmış şifreli metin</returns>
    string Encrypt(string plainText);
    
    /// <summary>
    /// Geriye dönük uyumluluk için senkron çözme metodu
    /// </summary>
    /// <param name="cipherText">Base64 kodlanmış şifreli metin</param>
    /// <returns>Çözülmüş düz metin</returns>
    string Decrypt(string cipherText);
    
    /// <summary>
    /// E-posta aktivasyonu için güvenli token oluşturur
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="email">Kullanıcı e-posta adresi</param>
    /// <returns>Şifrelenmiş token</returns>
    Task<string> GenerateActivationTokenAsync(string userId, string email);
    
    /// <summary>
    /// Geriye dönük uyumluluk için senkron aktivasyon token oluşturma metodu
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="email">Kullanıcı e-posta adresi</param>
    /// <returns>Şifrelenmiş token</returns>
    string GenerateActivationToken(string userId, string email);
    
    /// <summary>
    /// Aktivasyon tokenını doğrular
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="email">Kullanıcı e-posta adresi</param>
    /// <param name="token">Doğrulanacak token</param>
    /// <returns>Token geçerli ise true, değilse false</returns>
    Task<bool> VerifyActivationTokenAsync(string userId, string email, string token);
    
    /// <summary>
    /// Şifre için güvenli karma (hash) oluşturur
    /// </summary>
    /// <param name="password">Karma oluşturulacak şifre</param>
    /// <param name="salt">Kullanılacak tuz değeri (null ise rastgele oluşturulur)</param>
    /// <returns>Tuz ve karma kombinasyonu</returns>
    string HashPassword(string password, string salt = null);
}