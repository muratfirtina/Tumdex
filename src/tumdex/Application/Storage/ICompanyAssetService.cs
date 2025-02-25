using Application.Dtos.Assets;
using Microsoft.AspNetCore.Http;

namespace Application.Storage;

public interface ICompanyAssetService
{
    Task<bool> UploadLogoAsync(IFormFile file);
    Task<CompanyLogoDto> GetLogoAsync();
    Task<bool> UpdateLogoAsync(IFormFile file);
}