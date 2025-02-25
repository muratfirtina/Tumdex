using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.PhoneNumbers.Commands.Create;
using Application.Features.PhoneNumbers.Commands.DefaultPhoneNumber;
using Application.Features.PhoneNumbers.Commands.Delete;
using Application.Features.PhoneNumbers.Commands.Update;
using Application.Features.PhoneNumbers.Queries.GetList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Admin")]
    public class PhoneNumbersController : BaseController
    {
        [HttpGet]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get List Phone Numbers", Menu = AuthorizeDefinitionConstants.PhoneNumbers)]
        public async Task<IActionResult> GetList()
        {
            var response = await Mediator.Send(new GetListPhoneNumberQuery());
            return Ok(response);
        }

        [HttpPost]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Add Phone Number", Menu = AuthorizeDefinitionConstants.PhoneNumbers)]
        public async Task<IActionResult> Add([FromBody] CreatePhoneNumberCommand createPhoneNumberCommand)
        {
            var response = await Mediator.Send(createPhoneNumberCommand);
            return Ok(response);
        }

        [HttpPut("{id}")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Phone Number", Menu = AuthorizeDefinitionConstants.PhoneNumbers)]
        public async Task<IActionResult> Update([FromBody] UpdatePhoneNumberCommand updatePhoneNumberCommand)
        {
            var response = await Mediator.Send(updatePhoneNumberCommand);
            return Ok(response);
        }

        [HttpDelete("{id}")]
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete Phone Number", Menu = AuthorizeDefinitionConstants.PhoneNumbers)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            var response = await Mediator.Send(new DeletePhoneNumberCommand { Id = id });
            return Ok(response);
        }

        [HttpPut("{id}/set-default")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Set Default Phone Number", Menu = AuthorizeDefinitionConstants.PhoneNumbers)]
        public async Task<IActionResult> SetDefault([FromRoute] string id)
        {
            var response = await Mediator.Send(new SetDefaultPhoneNumberCommand { Id = id });
            return Ok(response);
        }
    }
}
