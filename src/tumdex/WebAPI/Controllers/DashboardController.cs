using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Dashboard.Queries.GetDashboardStatistics;
using Application.Features.Dashboard.Queries.GetRecentBrands;
using Application.Features.Dashboard.Queries.GetRecentCategories;
using Application.Features.Dashboard.Queries.GetTopCartProducts;
using Application.Features.Dashboard.Queries.GetTopOrderLocations;
using Application.Features.Dashboard.Queries.GetTopSellingProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Admin")]
    public class DashboardController : BaseController
    {
        [HttpGet("statistics")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Dashboard Statistics", Menu = AuthorizeDefinitionConstants.Dashboard)]
        public async Task<IActionResult> GetStatistics([FromQuery] string timeFrame = "all")
        {
            var query = new GetDashboardStatisticsQuery { TimeFrame = timeFrame };
            var response = await Mediator.Send(query);
            return Ok(response.Statistics);
        }

        [HttpGet("top-selling-products")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Top Selling Products", Menu = AuthorizeDefinitionConstants.Dashboard)]
        public async Task<IActionResult> GetTopSellingProducts([FromQuery] string timeFrame = "all", [FromQuery] int count = 10)
        {
            var query = new GetTopSellingProductsQuery { TimeFrame = timeFrame, Count = count };
            var response = await Mediator.Send(query);
            return Ok(response.Products);
        }

        [HttpGet("top-cart-products")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Top Cart Products", Menu = AuthorizeDefinitionConstants.Dashboard)]
        public async Task<IActionResult> GetTopCartProducts([FromQuery] string timeFrame = "all", [FromQuery] int count = 10)
        {
            var query = new GetTopCartProductsQuery { TimeFrame = timeFrame, Count = count };
            var response = await Mediator.Send(query);
            return Ok(response.Products);
        }

        [HttpGet("top-order-locations")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Top Order Locations", Menu = AuthorizeDefinitionConstants.Dashboard)]
        public async Task<IActionResult> GetTopOrderLocations([FromQuery] string timeFrame = "all", [FromQuery] int count = 10)
        {
            var query = new GetTopOrderLocationsQuery { TimeFrame = timeFrame, Count = count };
            var response = await Mediator.Send(query);
            return Ok(response.Locations);
        }

        [HttpGet("recent-categories")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Recent Categories", Menu = AuthorizeDefinitionConstants.Dashboard)]
        public async Task<IActionResult> GetRecentCategories([FromQuery] int count = 5)
        {
            var query = new GetRecentCategoriesQuery { Count = count };
            var response = await Mediator.Send(query);
            return Ok(response.Categories);
        }

        [HttpGet("recent-brands")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Recent Brands", Menu = AuthorizeDefinitionConstants.Dashboard)]
        public async Task<IActionResult> GetRecentBrands([FromQuery] int count = 5)
        {
            var query = new GetRecentBrandsQuery { Count = count };
            var response = await Mediator.Send(query);
            return Ok(response.Brands);
        }
    }
}