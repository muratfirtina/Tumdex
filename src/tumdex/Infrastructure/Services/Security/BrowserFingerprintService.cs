using System.Security.Cryptography;
using System.Text;
using Application.Abstraction.Services.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Security;

public class BrowserFingerprintService : IBrowserFingerprintService
{
    private readonly ILogger<BrowserFingerprintService> _logger;

    public BrowserFingerprintService(ILogger<BrowserFingerprintService> logger)
    {
        _logger = logger;
    }

    public string GenerateFingerprint(HttpContext httpContext)
    {
        try
        {
            // Parmak izi için özellikleri topla
            var fingerprintComponents = new List<string>
            {
                httpContext.Request.Headers["User-Agent"].ToString(),
                httpContext.Request.Headers["Accept-Language"].ToString(),
                httpContext.Request.Headers["Accept-Encoding"].ToString(),
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0"
            };
            
            // İsteğe bağlı: daha fazla özellik eklenebilir:
            // - Ekran çözünürlüğü (JavaScript ile frontend'den alınması gerekir)
            // - Yüklü fontlar (JavaScript ile frontend'den alınması gerekir)
            // - İşletim sistemi ve tarayıcı sürümü (User-Agent parsing ile elde edilebilir)
            
            // Bileşenleri birleştir ve hash'le
            string fingerprintData = string.Join("|", fingerprintComponents);
            using (var sha256 = SHA256.Create())
            {
                var fingerprintBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprintData));
                return Convert.ToBase64String(fingerprintBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tarayıcı parmak izi oluşturma hatası");
            
            // Hata durumunda yedek strateji (IP + User Agent)
            try 
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
                var ua = httpContext.Request.Headers["User-Agent"].ToString();
                
                using (var sha256 = SHA256.Create())
                {
                    var fingerprintBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{ip}|{ua}"));
                    return Convert.ToBase64String(fingerprintBytes);
                }
            }
            catch
            {
                // Son çare rastgele bir değer döndür
                return Guid.NewGuid().ToString();
            }
        }
    }

    public bool ValidateFingerprint(HttpContext httpContext, string storedFingerprint)
    {
        if (string.IsNullOrEmpty(storedFingerprint))
        {
            return false;
        }
        
        // Mevcut parmak izini oluştur ve karşılaştır
        string currentFingerprint = GenerateFingerprint(httpContext);
        
        // Tam eşleşme yerine benzerlik skoru kontrol edilebilir
        // Bu, tarayıcı güncellemeleri vb. durumlarında da çalışmaya devam etmesini sağlar
        bool isMatch = currentFingerprint == storedFingerprint;
        
        if (!isMatch)
        {
            _logger.LogWarning("Parmak izi eşleşmedi: Mevcut={Current}, Beklenen={Expected}",
                currentFingerprint, storedFingerprint);
        }
        
        return isMatch;
    }
}