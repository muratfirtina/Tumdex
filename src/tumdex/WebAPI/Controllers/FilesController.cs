using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : BaseController
    {
        private readonly string _wwwrootPath;
    
        public FilesController(IWebHostEnvironment environment)
        {
            _wwwrootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }
    
        [HttpGet("{entityType}/{path}/{fileName}")]
        public async Task<IActionResult> GetFile(string entityType, string path, string fileName)
        {
            var filePath = Path.Combine(_wwwrootPath, entityType, path, fileName);
        
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }
        
            var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = GetContentType(fileExtension);
        
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, contentType);
        }
    
        private string GetContentType(string fileExtension)
        {
            return fileExtension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".avif" => "image/avif",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }
    }
}
