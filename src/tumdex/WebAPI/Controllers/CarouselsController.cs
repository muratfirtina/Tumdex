using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Carousels.Commands.Create;
using Application.Features.Carousels.Commands.Update;
using Application.Features.Carousels.Queries.GetCarousel;
using Core.Application.Requests;
using Core.Application.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CarouselsController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetAllCarouselQueryResponse> response = await Mediator.Send(new GetAllCarouselQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        
        [HttpPost]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Writing, Definition = "Create Carousel", Menu = AuthorizeDefinitionConstants.Carousels)]
        public async Task<IActionResult> Add([FromForm] CreateCarouselCommand createCarouselCommand)
        {
            CreatedCarouselResponse response = await Mediator.Send(createCarouselCommand);
            return Created(uri: "", response);
        }
        
        [HttpPut]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Carousel", Menu = AuthorizeDefinitionConstants.Carousels)]
        public async Task<IActionResult> Update([FromBody] UpdateCarouselCommand updateCarouselCommand)
        {
            UpdatedCarouselResponse response = await Mediator.Send(updateCarouselCommand);
            return Ok(response);
        }
    }
}
