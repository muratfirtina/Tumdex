namespace Persistence.Models;

public class OutboxSettings
{
    public int RetentionDaysSuccess { get; set; } = 7;
    public int RetentionDaysFailed { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
    public int BatchSize { get; set; } = 50;
    public int CleanupBatchSize { get; set; } = 1000;
}