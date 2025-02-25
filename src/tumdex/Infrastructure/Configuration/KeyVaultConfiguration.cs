using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Configuration;

public static class KeyVaultConfiguration
{
    public static string GetSecretFromKeyVault(this IConfiguration configuration, string secretName, string defaultValue = "")
    {
        var value = configuration[secretName];
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    // Key Vault yapılandırmasını ekleyen metod
    public static WebApplicationBuilder AddKeyVaultConfiguration(this WebApplicationBuilder builder)
    {
        var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI");
        Console.WriteLine($"Key Vault URI: {keyVaultUri}"); // Debug için

        if (string.IsNullOrEmpty(keyVaultUri))
        {
            keyVaultUri = builder.Configuration["AZURE_KEYVAULT_URI"] ?? 
                          builder.Configuration["AzureKeyVault:VaultUri"];
        }
    
        if (string.IsNullOrEmpty(keyVaultUri))
        {
            throw new InvalidOperationException("""
                                                Key Vault URI bulunamadı!
                                                Lütfen aşağıdaki kontrolleri yapın:
                                                1. .env dosyasında AZURE_KEYVAULT_URI değişkeninin tanımlı olduğundan emin olun
                                                2. appsettings.json dosyasında AzureKeyVault:VaultUri değerinin doğru olduğundan emin olun
                                                3. Environment variable olarak AZURE_KEYVAULT_URI'nin tanımlı olduğundan emin olun

                                                Örnek: AZURE_KEYVAULT_URI=https://your-vault-name.vault.azure.net/
                                                """);
        }
    
        try
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
            
                Diagnostics =
                {
                    LoggedHeaderNames = { "x-ms-request-id" },
                    LoggedQueryParameters = { "api-version" },
                    IsLoggingEnabled = true
                }
            });

            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri), 
                credential);

            // Test the connection
            ValidateKeyVaultConnection(builder.Configuration);

            return builder;
        }
        catch (Exception ex)
        {
            HandleKeyVaultConfigurationError(ex, keyVaultUri);
            throw;
        }
    }

    // Key Vault bağlantısını doğrula
    private static void ValidateKeyVaultConnection(IConfiguration configuration)
    {
        var testSecret = configuration["JwtSecurityKey"];
        if (string.IsNullOrEmpty(testSecret))
        {
            throw new InvalidOperationException(
                """
                Key Vault bağlantısı başarılı ancak gizli değerlere erişilemiyor.
                Lütfen şunları kontrol edin:
                1. RBAC rolleri doğru atanmış mı?
                2. Gizli değerler Key Vault'a yüklenmiş mi?
                3. Gizli değerlerin isimleri doğru mu?
                """);
        }
    }

    // Hata durumunu yönet
    private static void HandleKeyVaultConfigurationError(Exception ex, string keyVaultUri)
    {
        var errorMessage = $"""
            Key Vault yapılandırması başarısız oldu!
            
            Olası nedenler ve çözümler:
            1. Azure CLI ile giriş yapılmamış olabilir
               Çözüm: Terminal'de 'az login' komutunu çalıştırın
               
            2. Key Vault URI yanlış olabilir
               Kontrol edilecek URI: {keyVaultUri}
               
            3. RBAC rolleri eksik olabilir
               Gerekli rol: Key Vault Secrets User
               Çözüm: Azure Portal'dan rol atamasını kontrol edin
               
            4. Ağ erişimi engellenmiş olabilir
               Key Vault güvenlik duvarı ayarlarını kontrol edin
            
            Teknik Detaylar:
            {ex.Message}
            
            Stack Trace:
            {ex.StackTrace}
            """;
        
        throw new InvalidOperationException(errorMessage, ex);
    }
}