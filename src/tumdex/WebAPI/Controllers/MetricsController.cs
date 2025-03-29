using Application.Abstraction.Services.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MetricsController : ControllerBase
    {
        private readonly IMetricsService _metricsService;
        
        public MetricsController(IMetricsService metricsService)
        {
            _metricsService = metricsService;
        }
        
        [HttpGet]
        public IActionResult GetMetrics()
        {
            var metrics = _metricsService.GetCurrentMetrics();
            return Ok(metrics);
        }
    }
}
