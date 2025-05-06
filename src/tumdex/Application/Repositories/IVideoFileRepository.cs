using Core.Persistence.Repositories;
using Domain.Entities;

namespace Application.Repositories;

public interface IVideoFileRepository : IAsyncRepository<VideoFile, string>, IRepository<VideoFile, string>
{
    // Video dosyalarına özgü metodlar buraya eklenebilir
    Task<List<VideoFile>> GetVideosByEntityIdAsync(string entityId, string entityType);
    Task<VideoFile> GetVideoByIdAsync(string id);
    Task<bool> SetPrimaryVideoAsync(string entityId, string videoFileId);
}