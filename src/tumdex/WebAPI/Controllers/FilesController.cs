using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : BaseController
    {
        private readonly IWebHostEnvironment _environment;

        public FilesController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpGet("{*filePath}")]
        public IActionResult GetFile(string filePath)
        {
            var fullPath = Path.Combine(_environment.ContentRootPath, "wwwroot", filePath);
            
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }
            
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".avif" => "image/avif",
                ".heic" => "image/heic",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
            
            // Önbelleğe alma başlıkları
            Response.Headers.Add("Cache-Control", "public, max-age=86400"); // 1 gün
            
            return PhysicalFile(fullPath, contentType);
        }
    }
}