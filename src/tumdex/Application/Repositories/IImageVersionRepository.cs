using Domain;

namespace Application.Repositories;

public interface IImageVersionRepository
{
    Task<List<ImageVersion>> GetVersionsByImageId(string imageId);
    Task<List<ImageVersion>> GetVersionsByFormat(string format);
}