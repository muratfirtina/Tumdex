using Application.Consts;
using Application.CustomAttributes;
using Application.Dtos.User;
using Application.Enums;
using Application.Features.UserAddresses.Commands.Create;
using Application.Features.UserAddresses.Commands.DefaultAddress;
using Application.Features.UserAddresses.Commands.Delete;
using Application.Features.UserAddresses.Commands.Update;
using Application.Features.UserAddresses.Dtos;
using Application.Features.UserAddresses.Queries.GetList;
using Application.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Admin")]
    public class UserAddressesController : BaseController
    {

        [HttpGet]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get List User Addresses", Menu = AuthorizeDefinitionConstants.UserAddresses)]
        public async Task<IActionResult> GetList()
        {
            var response = await Mediator.Send(new GetListUserAddressesQuery());
            return Ok(response);
        }
        
        

        [HttpPost]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Add User Address", Menu = AuthorizeDefinitionConstants.UserAddresses)]
        public async Task<IActionResult> AddAddress([FromBody] CreateUserAddressCommand createUserAddressCommand)
        {
            var response = await Mediator.Send(createUserAddressCommand);
            return Ok(response);
        }
        

        [HttpPut("{id}")] 
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update User Address", Menu = AuthorizeDefinitionConstants.UserAddresses)]
        public async Task<IActionResult> UpdateAddress([FromBody] UpdateUserAddressCommand updateUserAddressCommand)
        {
            var response = await Mediator.Send(updateUserAddressCommand);
            return Ok(response);
        }
           

        [HttpDelete("{id}")] 
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete User Address", Menu = AuthorizeDefinitionConstants.UserAddresses)]
        public async Task<IActionResult> DeleteAddress([FromRoute] string id)
        {
            var response = await Mediator.Send(new DeleteUserAddressCommand { Id = id });
            return Ok(response);
        }
        
        [HttpPut("{id}/set-default")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Set Default Address", Menu = AuthorizeDefinitionConstants.UserAddresses)]
        public async Task<IActionResult> SetDefaultAddress([FromRoute] string id)
        {
            var response = await Mediator.Send(new SetDefaultAddressCommand { Id = id });
            return Ok(response);
        }
    }
}
