using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Context;

namespace Persistence.Repositories;

public class ImageVersionRepository : EfRepositoryBase<ImageVersion, string, TumdexDbContext>, IImageVersionRepository
{
    public ImageVersionRepository(TumdexDbContext context) : base(context)
    {
    }

    public async Task<List<ImageVersion>> GetVersionsByImageId(string imageId)
    {
        return await Context.ImageVersions
            .Where(v => v.ImageFileId == imageId)
            .ToListAsync();
    }

    public async Task<List<ImageVersion>> GetVersionsByFormat(string format)
    {
        return await Context.ImageVersions
            .Where(v => v.Format == format)
            .ToListAsync();
    }
}