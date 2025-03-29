using Application.Consts;
using Application.CustomAttributes;
using Application.Dtos.Sitemap;
using Application.Enums;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    // 1. API endpoint'leri için controller
    [Route("api/[controller]")]
    [ApiController]
    public class SitemapsController : BaseController
    {
        private readonly ISitemapService _sitemapService;

        public SitemapsController(ISitemapService sitemapService)
        {
            _sitemapService = sitemapService;
        }

        [HttpGet("sitemap.xml")]
        public async Task<IActionResult> GetSitemapIndex()
        {
            var sitemap = await _sitemapService.GenerateSitemapIndex();
            if (string.IsNullOrEmpty(sitemap))
            {
                return NotFound("No sitemaps available - site might be empty");
            }
            return Content(sitemap, "application/xml");
        }

        [HttpGet("products.xml")]
        public async Task<IActionResult> GetProductSitemap()
        {
            var sitemap = await _sitemapService.GenerateProductSitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("categories.xml")]
        public async Task<IActionResult> GetCategorySitemap()
        {
            var sitemap = await _sitemapService.GenerateCategorySitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("brands.xml")]
        public async Task<IActionResult> GetBrandSitemap()
        {
            var sitemap = await _sitemapService.GenerateBrandSitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("images.xml")]
        public async Task<IActionResult> GetImageSitemap()
        {
            var sitemap = await _sitemapService.GenerateImageSitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("static-pages.xml")]
        public async Task<IActionResult> GetStaticPagesSitemap()
        {
            var sitemap = await _sitemapService.GenerateStaticPagesSitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpPost("submit")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Submit Sitemaps", Menu = AuthorizeDefinitionConstants.Sitemaps)]
        public async Task<IActionResult> SubmitToSearchEngines()
        {
            await _sitemapService.SubmitSitemapsToSearchEngines();
            return Ok("Sitemaps submitted to search engines successfully");
        }
        
        [HttpGet("status")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Sitemap Status", Menu = AuthorizeDefinitionConstants.Sitemaps)]
        public async Task<ActionResult<SitemapMonitoringReport>> GetSitemapStatus()
        {
            try
            {
                var report = await _sitemapService.GetSitemapStatus();
                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new SitemapOperationResponse
                {
                    Success = false,
                    Message = "Failed to get sitemap status",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("refresh")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Refresh Sitemaps", Menu = AuthorizeDefinitionConstants.Sitemaps)]
        public async Task<ActionResult<SitemapOperationResponse>> RefreshSitemaps()
        {
            try
            {
                var result = await _sitemapService.RefreshSitemaps();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new SitemapOperationResponse
                {
                    Success = false,
                    Message = "Failed to refresh sitemaps",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }

    // 2. Doğrudan sitemap dosyalarına erişim için controller
    [Route("sitemaps")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)] // Tüm controller'ı Swagger'dan gizle
    public class SitemapFilesController : BaseController
    {
        private readonly ISitemapService _sitemapService;

        public SitemapFilesController(ISitemapService sitemapService)
        {
            _sitemapService = sitemapService;
        }

        [HttpGet("sitemap.xml")]
        public async Task<IActionResult> GetSitemapIndex()
        {
            var sitemap = await _sitemapService.GenerateSitemapIndex();
            if (string.IsNullOrEmpty(sitemap))
            {
                return NotFound("No sitemaps available - site might be empty");
            }
            return Content(sitemap, "application/xml");
        }

        [HttpGet("products-sitemap.xml")]
        public async Task<IActionResult> GetProductSitemap()
        {
            var sitemap = await _sitemapService.GenerateProductSitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("categories-sitemap.xml")]
        public async Task<IActionResult> GetCategorySitemap()
        {
            var sitemap = await _sitemapService.GenerateCategorySitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("brands-sitemap.xml")]
        public async Task<IActionResult> GetBrandSitemap()
        {
            var sitemap = await _sitemapService.GenerateBrandSitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("images-sitemap.xml")]
        public async Task<IActionResult> GetImageSitemap()
        {
            var sitemap = await _sitemapService.GenerateImageSitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("static-pages-sitemap.xml")]
        public async Task<IActionResult> GetStaticPagesSitemap()
        {
            var sitemap = await _sitemapService.GenerateStaticPagesSitemap();
            return Content(sitemap, "application/xml");
        }

        [HttpGet("status")]
        [Authorize(AuthenticationSchemes = "Admin")]
        public async Task<ActionResult<SitemapMonitoringReport>> GetSitemapStatus()
        {
            try
            {
                var report = await _sitemapService.GetSitemapStatus();
                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new SitemapOperationResponse
                {
                    Success = false,
                    Message = "Failed to get sitemap status",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("refresh")]
        [Authorize(AuthenticationSchemes = "Admin")]
        public async Task<ActionResult<SitemapOperationResponse>> RefreshSitemaps()
        {
            try
            {
                var result = await _sitemapService.RefreshSitemaps();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new SitemapOperationResponse
                {
                    Success = false,
                    Message = "Failed to refresh sitemaps",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}