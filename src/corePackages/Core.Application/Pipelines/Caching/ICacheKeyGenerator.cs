namespace Core.Application.Pipelines.Caching;

public interface ICacheKeyGenerator
{
    Task<string> GenerateKeyAsync(string baseKey, string? groupKey);
}