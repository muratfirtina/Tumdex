namespace Application.Abstraction.Services;

public interface IRateLimitService
{
    Task<(bool IsAllowed, int CurrentCount, TimeSpan? RetryAfter)> CheckRateLimitAsync(string key);
}