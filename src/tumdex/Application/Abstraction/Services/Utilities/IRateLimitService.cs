namespace Application.Abstraction.Services.Utilities;

public interface IRateLimitService
{
    Task<(bool IsAllowed, int CurrentCount, TimeSpan? RetryAfter)> CheckRateLimitAsync(string key);
}