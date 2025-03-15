using System.Threading.Channels;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;
    private readonly ILogger<BackgroundTaskQueue> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public BackgroundTaskQueue(
        int capacity, 
        ILogger<BackgroundTaskQueue> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(options);
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }

        // Arka plan görevini IServiceProvider ile sarmalayacak bir wrapper oluştur
        async Task WrappedWorkItem(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            try
            {
                // Arka plan görevi için yeni bir scope oluştur
                using var scope = _serviceScopeFactory.CreateScope();
                
                // Görev içinde özel bir parametre gerektirmeyen bir lambda oluştur
                Func<Task> scopedWorkItem = async () => await workItem(cancellationToken);
                
                // Scope içinde görevi çalıştır
                await scopedWorkItem();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Arka plan görevi çalıştırılırken hata oluştu");
            }
        }

        _queue.Writer.TryWrite(WrappedWorkItem);
    }

    public async Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}