using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.FeatureValues.Commands.Create;
using Application.Features.FeatureValues.Commands.Delete;
using Application.Features.FeatureValues.Commands.Update;
using Application.Features.FeatureValues.Queries.GetByDynamic;
using Application.Features.FeatureValues.Queries.GetById;
using Application.Features.FeatureValues.Queries.GetList;
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
    public class FeatureValuesController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetAllFeatureValueQueryResponse> response = await Mediator.Send(new GetAllFeatureValueQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById([FromRoute] string id)
        {
            GetByIdFeatureValueResponse response = await Mediator.Send(new GetByIdFeatureValueQuery { Id = id });
            return Ok(response);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Create FeatureValue", Menu = AuthorizeDefinitionConstants.FeatureValues)]
        public async Task<IActionResult> Add([FromBody] CreateFeatureValueCommand createFeatureValueCommand)
        {
            CreatedFeatureValueResponse response = await Mediator.Send(createFeatureValueCommand);

            return Created(uri: "", response);
        }
        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete FeatureValue", Menu = AuthorizeDefinitionConstants.FeatureValues)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            DeletedFeatureValueResponse response = await Mediator.Send(new DeleteFeatureValueCommand { Id = id });
            return Ok(response);
        }
        [HttpPut]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update FeatureValue", Menu = AuthorizeDefinitionConstants.FeatureValues)]
        public async Task<IActionResult> Update([FromBody] UpdateFeatureValueCommand updateFeatureValueCommand)
        {
            UpdatedFeatureValueResponse response = await Mediator.Send(updateFeatureValueCommand);
            return Ok(response);
        }
        [HttpPost("GetList/ByDynamic")]
        public async Task<IActionResult> GetListByDynamic([FromQuery] PageRequest pageRequest, [FromBody] DynamicQuery? dynamicQuery = null)
        {
            GetListResponse<GetListFeatureValueByDynamicDto> response = await Mediator.Send(new GetListFeatureValueByDynamicQuery { PageRequest = pageRequest, DynamicQuery = dynamicQuery });
            return Ok(response);
        }
    }
}
