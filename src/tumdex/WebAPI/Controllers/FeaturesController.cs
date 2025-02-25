using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Features.Commands.Create;
using Application.Features.Features.Commands.Delete;
using Application.Features.Features.Commands.Update;
using Application.Features.Features.Queries.GetByDynamic;
using Application.Features.Features.Queries.GetById;
using Application.Features.Features.Queries.GetList;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeaturesController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetAllFeatureQueryResponse> response = await Mediator.Send(new GetAllFeatureQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById([FromRoute] string id)
        {
            GetByIdFeatureResponse response = await Mediator.Send(new GetByIdFeatureQuery { Id = id });
            return Ok(response);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Create Feature", Menu = AuthorizeDefinitionConstants.Features)]
        
        public async Task<IActionResult> Add([FromBody] CreateFeatureCommand createFeatureCommand)
        {
            CreatedFeatureResponse response = await Mediator.Send(createFeatureCommand);

            return Created(uri: "", response);
        }
        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete Feature", Menu = AuthorizeDefinitionConstants.Features)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            DeletedFeatureResponse response = await Mediator.Send(new DeleteFeatureCommand { Id = id });
            return Ok(response);
        }
        [HttpPut]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Feature", Menu = AuthorizeDefinitionConstants.Features)]
        public async Task<IActionResult> Update([FromBody] UpdateFeatureCommand updateFeatureCommand)
        {
            UpdatedFeatureResponse response = await Mediator.Send(updateFeatureCommand);
            return Ok(response);
        }
        [HttpPost("GetList/ByDynamic")]
        public async Task<IActionResult> GetListByDynamic([FromQuery] PageRequest pageRequest, [FromBody] DynamicQuery? dynamicQuery = null)
        {
            GetListResponse<GetListFeatureByDynamicDto> response = await Mediator.Send(new GetListFeatureByDynamicQuery { PageRequest = pageRequest, DynamicQuery = dynamicQuery });
            return Ok(response);
        }
    }
}
