using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstraction.Services;

public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Arka planda çalıştırılacak bir iş parçacığını kuyruğa ekler
    /// </summary>
    /// <param name="workItem">Arka planda çalıştırılacak iş parçacığı</param>
    void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

    /// <summary>
    /// Kuyruktan bir sonraki iş parçacığını alır
    /// </summary>
    /// <param name="cancellationToken">İptal belirteci</param>
    /// <returns>Servis sağlayıcı ve iptal belirteci alan bir iş parçacığı</returns>
    Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}