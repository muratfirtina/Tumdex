using Application.Storage;

namespace Infrastructure.Services.Storage;

public interface IStorageProviderFactory
{
    IStorageProvider GetProvider(string? providerName = null);
    IEnumerable<IStorageProvider> GetConfiguredProviders();
}