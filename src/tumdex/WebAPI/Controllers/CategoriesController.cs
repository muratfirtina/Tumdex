using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Categories.Commands.Create;
using Application.Features.Categories.Commands.Delete;
using Application.Features.Categories.Commands.Update;
using Application.Features.Categories.Queries.GetByDynamic;
using Application.Features.Categories.Queries.GetById;
using Application.Features.Categories.Queries.GetCategoriesByIds;
using Application.Features.Categories.Queries.GetList;
using Application.Features.Categories.Queries.GetMainCategories;
using Application.Features.Categories.Queries.GetSubCategoriesByBrandId;
using Application.Features.Categories.Queries.GetSubCategoriesByCategoryId;
using Application.Features.Categories.Queries.Search;
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
    public class CategoriesController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetAllCategoryQueryResponse> response = await Mediator.Send(new GetAllCategoryQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById([FromRoute] string id)
        {
            GetByIdCategoryResponse response = await Mediator.Send(new GetByIdCategoryQuery { Id = id });
            return Ok(response);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Create Category", Menu = AuthorizeDefinitionConstants.Categories)]
        public async Task<IActionResult> Add([FromForm] CreateCategoryCommand createCategoryCommand)
        {
            CreatedCategoryResponse response = await Mediator.Send(createCategoryCommand);

            return Created(uri: "", response);
        }
        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Deleting, Definition = "Delete Category", Menu = AuthorizeDefinitionConstants.Categories)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            DeletedCategoryResponse response = await Mediator.Send(new DeleteCategoryCommand { Id = id });
            return Ok(response);
        }
        [HttpPut]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Category", Menu = AuthorizeDefinitionConstants.Categories)]
        public async Task<IActionResult> Update([FromForm] UpdateCategoryCommand updateCategoryCommand)
        {
            UpdatedCategoryResponse response = await Mediator.Send(updateCategoryCommand);
            return Ok(response);
        }

        [HttpPost("GetList/ByDynamic")]
        public async Task<IActionResult> GetListByDynamic([FromQuery] PageRequest pageRequest, [FromBody] DynamicQuery? dynamicQuery = null)
        {
            GetListResponse<GetListCategoryByDynamicDto> response = await Mediator.Send(new GetListCategoryByDynamicQuery { PageRequest = pageRequest, DynamicQuery = dynamicQuery });
            return Ok(response);
        }
        [HttpGet("GetMainCategories")]
        public async Task<IActionResult> GetMainCategories([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetMainCategoriesResponse> response = await Mediator.Send(new GetMainCategoiesQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        
        [HttpPost("GetByIds")]
        
        public async Task<IActionResult> GetByIds([FromBody] List<string> ids)
        {
            GetListResponse<GetCategoriesByIdsQueryResponse> response = await Mediator.Send(new GetCategoriesByIdsQuery { Ids = ids });
            return Ok(response);
        }
        
        [HttpGet("GetSubCategoriesByCategoryId/{parentCategoryId}")]
        public async Task<IActionResult> GetSubCategoriesByCategoryId([FromRoute] string parentCategoryId)
        {
            GetListResponse<GetSubCategoriesByCategoryIdQueryReponse> response = await Mediator.Send(new GetSubCategoriesByCategoryIdQuery { ParentCategoryId = parentCategoryId });
            return Ok(response);
        }
        
        [HttpGet("GetSubCategoriesByBrandId/{brandId}")]
        public async Task<IActionResult> GetSubCategoriesByBrandId([FromRoute] string brandId)
        {
            GetListResponse<GetSubCategoriesByBrandIdQueryReponse> response = await Mediator.Send(new GetSubCategoriesByBrandIdQuery { BrandId = brandId});
            return Ok(response);
        }
        
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string searchTerm)
        {
            var response = await Mediator.Send(new SearchCategoryQuery { SearchTerm = searchTerm });
            return Ok(response);
        }

    }
}
