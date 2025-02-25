using Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : BaseController
    {
        private readonly IImageSeoService _imageSeoService;
        private readonly ISitemapService _sitemapService;

        public ImagesController(IImageSeoService imageSeoService, ISitemapService sitemapService)
        {
            _imageSeoService = imageSeoService;
            _sitemapService = sitemapService;
        }

        [HttpGet("sitemap.xml")]
        public async Task<IActionResult> GetImageSitemap()
        {
            var sitemap = await _sitemapService.GenerateImageSitemap();
            return Content(sitemap, "application/xml");
        }
    }
}
