using Domain.Entities;

namespace Application.Abstraction.Services.Utilities;

public interface ILogService
{
    Task<SecurityLog> CreateLogAsync(SecurityLog log);
    Task<List<SecurityLog>> GetLogsByDateRangeAsync(DateTime start, DateTime end);
    Task<List<SecurityLog>> GetLogsByLevelAsync(string level);
    Task<List<SecurityLog>> GetLogsByIPAsync(string clientIP);
    Task<List<SecurityLog>> GetLogsByEventTypeAsync(string eventType);
    Task<List<SecurityLog>> GetLogsByUserAsync(string userName);
}

