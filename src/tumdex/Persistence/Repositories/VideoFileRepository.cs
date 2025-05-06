using Persistence.Context;
using Application.Repositories;
using Core.Persistence.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Repositories;

public class VideoFileRepository : EfRepositoryBase<VideoFile, string, TumdexDbContext>, IVideoFileRepository
{
    public VideoFileRepository(TumdexDbContext context) : base(context)
    {
    }

    public async Task<List<VideoFile>> GetVideosByEntityIdAsync(string entityId, string entityType)
    {
        return await Context.Set<VideoFile>()
            .Where(v => v.Path == entityId && v.EntityType == entityType)
            .OrderByDescending(v => v.CreatedDate)
            .ToListAsync();
    }

    public async Task<VideoFile> GetVideoByIdAsync(string id)
    {
        return await Context.Set<VideoFile>()
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<bool> SetPrimaryVideoAsync(string entityId, string videoFileId)
    {
        // Bu metot, belirli bir varlığa ait videolardan birini birincil video olarak işaretler
        // Örneğin: carousel'da gösterilecek ana video
        
        try
        {
            // Önce ilgili varlığa ait tüm videoları al
            var videos = await Context.Set<VideoFile>()
                .Where(v => v.Path == entityId)
                .ToListAsync();

            // Birincil video özelliğini ayarla (Bu özelliği VideoFile sınıfına eklememiz gerekecek)
            foreach (var video in videos)
            {
                // Burada VideoFile sınıfında IsPrimary gibi bir alan olduğunu varsayıyoruz
                video.IsPrimary = (video.Id == videoFileId);
            }

            await Context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}