using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using Azure.Security.KeyVault.Secrets;

namespace Persistence.DbConfiguration;

/// <summary>
/// Veritabanı bağlantı bilgilerini Azure Key Vault'tan güvenli bir şekilde almaktan sorumlu sınıf.
/// </summary>
public static class DatabaseConfiguration
{
    public static string GetConnectionString(IConfiguration configuration, ILogger logger)
    {
        try
        {
            // Key Vault URI'yi environment variables'dan al
            var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI") ?? 
                              throw new InvalidOperationException("AZURE_KEYVAULT_URI not found");

            // Azure credential oluştur
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeManagedIdentityCredential = false
            });

            // Key Vault client oluştur
            var client = new SecretClient(new Uri(keyVaultUri), credential);

            // Database bağlantı bilgilerini Key Vault'tan al
            var postgresUser = client.GetSecret("PostgresUser").Value.Value;
            var postgresPassword = client.GetSecret("PostgresPassword").Value.Value;
            var postgresHost = client.GetSecret("PostgresHost").Value.Value;
            var postgresPort = client.GetSecret("PostgresPort").Value.Value;
            var postgresDatabase = client.GetSecret("PostgresDatabase").Value.Value;

            // Connection string oluştur
            return $"Server={postgresHost};" +
                   $"Port={postgresPort};" +
                   $"Database={postgresDatabase};" +
                   $"User Id={postgresUser};" +
                   $"Password={postgresPassword};" +
                   "Enlist=true;" +
                   "Pooling=true;";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Veritabanı bağlantı bilgileri alınamadı");
            throw new InvalidOperationException(
                "Veritabanı bağlantı bilgileri alınamadı. Detaylar: " + ex.Message, 
                ex);
        }
    }
    
}