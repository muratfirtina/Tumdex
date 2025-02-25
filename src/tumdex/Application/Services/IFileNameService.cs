using Microsoft.AspNetCore.Http;

namespace Application.Services;

public interface IFileNameService
{
    public delegate bool HasFile(string pathOrContainerName, string fileName);
    Task<string> FileRenameAsync(string pathOrContainerName,string fileName, HasFile hasFileMethod);
    Task<string> PathRenameAsync(string pathOrContainerName);
    Task FileMustBeInFileFormat(IFormFile formFile);
}