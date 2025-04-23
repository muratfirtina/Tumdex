using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("robots.txt")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class RobotsController : BaseController
    {
        private readonly IConfiguration _configuration;

        public RobotsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public ContentResult GetRobotsTxt()
        {
            string baseUrl = _configuration["WebAPIConfiguration:APIDomain:0"];

            string robotsTxt = $@"User-agent: *
Allow: /
Disallow: /api/auth/
Disallow: /api/admin/
Disallow: /panel/
Disallow: /basket/
Disallow: /checkout/
Disallow: /user/

# Sitemap dosyaları
Sitemap: {baseUrl}/sitemap.xml
Sitemap: {baseUrl}/sitemaps/sitemap.xml
";

            return Content(robotsTxt, "text/plain");
        }
    }
}