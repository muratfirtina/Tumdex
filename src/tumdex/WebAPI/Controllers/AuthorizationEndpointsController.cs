using Application.Features.AuthorizationEndpoint.Commands.AssignRoleToEndpoint;
using Application.Features.AuthorizationEndpoint.Queries.GetRolesToEndpoint;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorizationEndpointsController : BaseController
    {
        [HttpPost("get-roles-to-endpoint")]
        public async Task<IActionResult> GetRolesToEndpoint([FromBody]GetRolesToEndpointQuery getRolesToEndpointQuery)
        {
            var response = await Mediator.Send(getRolesToEndpointQuery);
            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> AssignRoleToEndpoint(AssignRoleToEndpointRequest assignRoleToEndpointRequest)
        {
            assignRoleToEndpointRequest.Type = typeof(Program);
            var response = await Mediator.Send(assignRoleToEndpointRequest);
            return Ok(response);
        }
    }
}
