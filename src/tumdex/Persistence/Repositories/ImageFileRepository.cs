using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Persistence.Context;

namespace Persistence.Repositories;

public class ImageFileRepository : EfRepositoryBase<ImageFile, string, TumdexDbContext>, IImageFileRepository
{
    public ImageFileRepository(TumdexDbContext context) : base(context)
    {
    }
    public async Task AddAsync(List<ProductImageFile> toList)
    {
        await Context.Set<ProductImageFile>().AddRangeAsync(toList);
        await Context.SaveChangesAsync();
        
    }
}