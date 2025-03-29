using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Brands.Commands.Create;
using Application.Features.Brands.Commands.Delete;
using Application.Features.Brands.Commands.Update;
using Application.Features.Brands.Queries.GetBrandsByIds;
using Application.Features.Brands.Queries.GetByDynamic;
using Application.Features.Brands.Queries.GetById;
using Application.Features.Brands.Queries.GetList;
using Application.Features.Brands.Queries.Search;
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
    public class BrandsController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetAllBrandQueryResponse> response = await Mediator.Send(new GetAllBrandQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById([FromRoute] string id)
        {
            GetByIdBrandResponse response = await Mediator.Send(new GetByIdBrandQuery { Id = id });
            return Ok(response);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Create Brand", Menu = AuthorizeDefinitionConstants.Brands)]
        public async Task<IActionResult> Add([FromForm] CreateBrandCommand createBrandCommand)
        {
            CreatedBrandResponse response = await Mediator.Send(createBrandCommand);
            return Created(uri: "", response);
        }
        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete Brand", Menu = AuthorizeDefinitionConstants.Brands)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            DeletedBrandResponse response = await Mediator.Send(new DeleteBrandCommand { Id = id });
            if (response.Success)
            {
                return Ok(response);
            }
            return BadRequest("Brand deletion failed.");
        }
        [HttpPut]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Brand", Menu = AuthorizeDefinitionConstants.Brands)]
        public async Task<IActionResult> Update([FromForm] UpdateBrandCommand updateBrandCommand)
        {
            UpdatedBrandResponse response = await Mediator.Send(updateBrandCommand);
            return Ok(response);
        }
        [HttpPost("GetList/ByDynamic")]
        public async Task<IActionResult> GetListByDynamic([FromQuery] PageRequest pageRequest, [FromBody] DynamicQuery? dynamicQuery = null)
        {
            dynamicQuery ??= new DynamicQuery();
    
            GetListBrandByDynamicQuery query = new() 
            { 
                PageRequest = pageRequest, 
                DynamicQuery = dynamicQuery 
            };
    
            GetListResponse<GetListBrandByDynamicQueryResponse> response = await Mediator.Send(query);
            return Ok(response);
        }
        
        [HttpPost("GetByIds")]
        
        public async Task<IActionResult> GetByIds([FromBody] List<string> ids)
        {
            GetListResponse<GetBrandsByIdsQueryResponse> response = await Mediator.Send(new GetBrandsByIdsQuery { Ids = ids });
            return Ok(response);
        }
        
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? searchTerm)
        {
            var response = await Mediator.Send(new SearchBrandQuery { SearchTerm = searchTerm });
            return Ok(response);
        }
    }
}
