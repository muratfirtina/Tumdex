using Application.Abstraction.Services;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Persistence.Context;

namespace Infrastructure.Services;

public class LogService : ILogService
{
    private readonly TumdexDbContext _context;
    private readonly ILogger<LogService> _logger;

    public LogService(
        TumdexDbContext context,
        ILogger<LogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SecurityLog> CreateLogAsync(SecurityLog log)
    {
        try
        {
            await _context.SecurityLogs.AddAsync(log);
            await _context.SaveChangesAsync();
            return log;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create security log");
            throw;
        }
    }

    public async Task<List<SecurityLog>> GetLogsByDateRangeAsync(DateTime start, DateTime end)
    {
        return await _context.SecurityLogs
            .Where(l => l.Timestamp >= start && l.Timestamp <= end)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<SecurityLog>> GetLogsByLevelAsync(string level)
    {
        return await _context.SecurityLogs
            .Where(l => l.Level == level)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<SecurityLog>> GetLogsByIPAsync(string clientIP)
    {
        return await _context.SecurityLogs
            .Where(l => l.ClientIP == clientIP)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<SecurityLog>> GetLogsByEventTypeAsync(string eventType)
    {
        return await _context.SecurityLogs
            .Where(l => l.EventType == eventType)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<SecurityLog>> GetLogsByUserAsync(string userName)
    {
        return await _context.SecurityLogs
            .Where(l => l.UserName == userName)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }
}