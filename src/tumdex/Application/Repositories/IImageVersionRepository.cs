using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface IImageVersionRepository
{
    Task<List<ImageVersion>> GetVersionsByImageId(string imageId);
    Task<List<ImageVersion>> GetVersionsByFormat(string format);
}