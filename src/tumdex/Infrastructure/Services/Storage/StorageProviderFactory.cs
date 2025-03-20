using System.Diagnostics;
using Application.Storage;
using Application.Storage.Cloudinary;
using Application.Storage.Google;
using Application.Storage.Local;
using Application.Storage.Yandex;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage;

public class StorageProviderFactory : IStorageProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsSnapshot<StorageSettings> _storageSettings;
    private readonly IConfiguration _configuration;
    private readonly SecretClient _secretClient;

    public StorageProviderFactory(
        IServiceProvider serviceProvider,
        IOptionsSnapshot<StorageSettings> storageSettings, IConfiguration configuration, SecretClient secretClient)
    {
        _serviceProvider = serviceProvider;
        _storageSettings = storageSettings;
        _configuration = configuration;
        _secretClient = secretClient;

        var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI") ??
                          throw new InvalidOperationException("AZURE_KEYVAULT_URI not found");

        var credential = new DefaultAzureCredential();
        _secretClient = new SecretClient(new Uri(keyVaultUri), credential);
    }

    public IStorageProvider GetProvider(string? providerName = null)
    {
        // Okuma işlemleri için kullanılacak provider'ı belirle
        // Öncelik: Explicitly provided provider > appsettings.json ActiveProvider
        var activeProvider = providerName ?? _storageSettings.Value.ActiveProvider;

        // Active provider'a göre uygun provider'ı döndür
        return activeProvider?.ToLower() switch
        {
            "localstorage" => (IStorageProvider)_serviceProvider.GetRequiredService<ILocalStorage>(),
            "google" => (IStorageProvider)_serviceProvider.GetRequiredService<IGoogleStorage>(),
            _ => (IStorageProvider)_serviceProvider.GetRequiredService<ILocalStorage>() // Default provider
        };
    }

    public IEnumerable<IStorageProvider> GetConfiguredProviders()
    {
        // Yazma işlemleri için tüm konfigüre edilmiş provider'ları döndür
        var providers = new List<IStorageProvider>();

        if (HasValidUrl("LocalStorage"))
        {
            providers.Add((IStorageProvider)_serviceProvider.GetRequiredService<ILocalStorage>());
        }

        if (HasValidUrl("Google"))
        {
            try
            {
                providers.Add((IStorageProvider)_serviceProvider.GetRequiredService<IGoogleStorage>());
            }
            catch (Exception ex)
            {
                // Google Storage provider oluşturulurken hata olursa log'la ve devam et
                Debug.WriteLine($"Error initializing Google Storage provider: {ex.Message}");
            }
        }

        return providers;
    }

    private bool HasValidUrl(string providerName)
    {
        var providers = _storageSettings.Value.Providers;
        return providerName switch
        {
            "LocalStorage" => !string.IsNullOrEmpty(providers.LocalStorage?.Url),
            "Google" => !string.IsNullOrEmpty(providers.Google?.Url),
            _ => false
        };
    }
}