namespace Application.Abstraction.Services.Utilities;

public interface ICacheService
{
  
    
    // CancellationToken destekli yeni metotlar
    Task<(bool success, T value)> TryGetValueAsync<T>(string key, CancellationToken cancellationToken);
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken);
    Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken);
    Task SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry, CancellationToken cancellationToken);
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry, CancellationToken cancellationToken);
    Task<bool> IncrementAsync(string key, int value, TimeSpan? expiry, CancellationToken cancellationToken);
    Task<int> GetCounterAsync(string key, CancellationToken cancellationToken);
    Task<List<string>> GetKeysAsync(string pattern, CancellationToken cancellationToken);
}